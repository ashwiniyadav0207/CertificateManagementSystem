using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CertificateEngine.Configuration;
using CertificateEngine.Domain;

namespace CertificateEngine.Infrastructure.Sheets;

public sealed class GoogleSheetsSource : ISheetSource
{
    private readonly IGoogleSheetsClient _client;

    public GoogleSheetsSource(IGoogleSheetsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public async Task<IReadOnlyList<SheetSubmission>> ReadApprovedSubmissionsAsync(
        EventSourceOptions eventOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventOptions);
        ValidateEventIdentity(eventOptions);

        var address = SheetRangeAddress.Parse(eventOptions);
        var table = await _client.ReadValuesAsync(
            eventOptions.SpreadsheetId,
            address.RequestRange,
            cancellationToken);
        if (table.Rows.Count == 0)
        {
            throw new SourceDataException(
                $"Sheet '{eventOptions.SheetName}' for event '{eventOptions.EventId}' returned no header row " +
                $"from range '{eventOptions.Range}'.");
        }

        var headers = table.Rows[0];
        var headerMap = new HeaderMap(headers, address.StartColumnNumber);
        var sourceIdIndex = headerMap.Require(
            eventOptions.SourceIdColumn,
            nameof(eventOptions.SourceIdColumn),
            eventOptions);
        var nameIndex = headerMap.Require(
            eventOptions.NameColumn,
            nameof(eventOptions.NameColumn),
            eventOptions);
        var approvalIndex = headerMap.Require(
            eventOptions.ApprovalColumn,
            nameof(eventOptions.ApprovalColumn),
            eventOptions);
        var emailIndex = headerMap.FindOptional(
            eventOptions.EmailColumn,
            nameof(eventOptions.EmailColumn),
            eventOptions);
        var phoneIndex = headerMap.FindOptional(
            eventOptions.PhoneColumn,
            nameof(eventOptions.PhoneColumn),
            eventOptions);

        int? scoreIndex = null;
        if (!string.IsNullOrWhiteSpace(eventOptions.ScoreColumn))
        {
            scoreIndex = eventOptions.MinimumScore.HasValue
                ? headerMap.Require(
                    eventOptions.ScoreColumn,
                    nameof(eventOptions.ScoreColumn),
                    eventOptions)
                : headerMap.FindOptional(
                    eventOptions.ScoreColumn,
                    nameof(eventOptions.ScoreColumn),
                    eventOptions);
        }
        else if (eventOptions.MinimumScore.HasValue)
        {
            throw new SourceDataException(
                $"Event '{eventOptions.EventId}' sets MinimumScore but does not configure ScoreColumn.");
        }

        if (string.IsNullOrWhiteSpace(eventOptions.ApprovalValue))
        {
            throw new SourceDataException(
                $"Event '{eventOptions.EventId}' must configure a non-empty ApprovalValue.");
        }

        var submissions = new List<SheetSubmission>();
        for (var rowIndex = 1; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            if (!string.Equals(
                    GetCell(row, approvalIndex).Trim(),
                    eventOptions.ApprovalValue.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            decimal? score = null;
            if (scoreIndex.HasValue)
            {
                var scoreText = GetCell(row, scoreIndex.Value);
                if (TryParseScore(scoreText, out var parsedScore))
                {
                    score = parsedScore;
                }
            }

            if (eventOptions.MinimumScore.HasValue
                && (!score.HasValue || score.Value < eventOptions.MinimumScore.Value))
            {
                continue;
            }

            var rowNumber = address.HeaderRowNumber + rowIndex;
            var sourceId = GetCell(row, sourceIdIndex).Trim();
            if (sourceId.Length == 0)
            {
                throw MissingRequiredValue(eventOptions, rowNumber, eventOptions.SourceIdColumn);
            }

            var fullName = GetCell(row, nameIndex).Trim();
            if (fullName.Length == 0)
            {
                throw MissingRequiredValue(eventOptions, rowNumber, eventOptions.NameColumn);
            }

            var values = BuildValues(headers, row);
            submissions.Add(new SheetSubmission(
                eventOptions.EventId,
                eventOptions.EventName,
                eventOptions.SpreadsheetId,
                eventOptions.SheetName,
                rowNumber,
                new Participant(
                    sourceId,
                    fullName,
                    emailIndex.HasValue ? NullIfWhiteSpace(GetCell(row, emailIndex.Value)) : null,
                    phoneIndex.HasValue ? NullIfWhiteSpace(GetCell(row, phoneIndex.Value)) : null),
                ComputeSourceHash(headers, row, eventOptions),
                score,
                values));
        }

        return submissions.AsReadOnly();
    }

    public async Task WriteCertificateUpdatesAsync(
        EventSourceOptions eventOptions,
        IReadOnlyCollection<SheetCertificateUpdate> updates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventOptions);
        ArgumentNullException.ThrowIfNull(updates);
        if (updates.Count == 0)
        {
            return;
        }

        ValidateEventIdentity(eventOptions);
        ValidateLifecycleColumnNames(eventOptions);
        var address = SheetRangeAddress.Parse(eventOptions);
        ValidateUpdates(updates, address, eventOptions);

        var table = await _client.ReadValuesAsync(
            eventOptions.SpreadsheetId,
            address.RequestRange,
            cancellationToken);
        if (table.Rows.Count == 0)
        {
            throw new SourceDataException(
                $"Cannot write certificate results because sheet '{eventOptions.SheetName}' for event " +
                $"'{eventOptions.EventId}' has no header row in range '{eventOptions.Range}'.");
        }

        var headers = table.Rows[0];
        var headerMap = new HeaderMap(headers, address.StartColumnNumber);
        var writes = new List<SheetCellWrite>(3 + (updates.Count * 3));
        var nextRelativeColumn = headers.Count;

        int FindOrAppend(string headerName, string settingName)
        {
            var existing = headerMap.FindOptional(headerName, settingName, eventOptions);
            if (existing.HasValue)
            {
                return existing.Value;
            }

            var relativeColumn = nextRelativeColumn++;
            writes.Add(new SheetCellWrite(
                address.CellRange(relativeColumn, address.HeaderRowNumber),
                headerName.Trim()));
            return relativeColumn;
        }

        var statusIndex = FindOrAppend(
            eventOptions.CertificateStatusColumn,
            nameof(eventOptions.CertificateStatusColumn));
        var verificationIdIndex = FindOrAppend(
            eventOptions.VerificationIdColumn,
            nameof(eventOptions.VerificationIdColumn));
        var processedAtIndex = FindOrAppend(
            eventOptions.ProcessedAtColumn,
            nameof(eventOptions.ProcessedAtColumn));

        if (nextRelativeColumn > headers.Count)
        {
            var requiredColumnCount = address.StartColumnNumber + nextRelativeColumn - 1;
            await _client.EnsureColumnCapacityAsync(
                eventOptions.SpreadsheetId,
                eventOptions.SheetName,
                requiredColumnCount,
                cancellationToken);
        }

        foreach (var update in updates.OrderBy(candidate => candidate.RowNumber))
        {
            writes.Add(new SheetCellWrite(
                address.CellRange(statusIndex, update.RowNumber),
                update.CertificateStatus.Trim()));
            writes.Add(new SheetCellWrite(
                address.CellRange(verificationIdIndex, update.RowNumber),
                update.VerificationId?.Trim() ?? string.Empty));
            writes.Add(new SheetCellWrite(
                address.CellRange(processedAtIndex, update.RowNumber),
                update.ProcessedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        }

        await _client.BatchWriteValuesAsync(
            eventOptions.SpreadsheetId,
            writes.AsReadOnly(),
            cancellationToken);
    }

    private static void ValidateEventIdentity(EventSourceOptions eventOptions)
    {
        if (string.IsNullOrWhiteSpace(eventOptions.EventId))
        {
            throw new SourceDataException("The sheet source requires a non-empty EventId.");
        }

        if (string.IsNullOrWhiteSpace(eventOptions.EventName))
        {
            throw new SourceDataException(
                $"Event '{eventOptions.EventId}' requires a non-empty EventName.");
        }

        if (string.IsNullOrWhiteSpace(eventOptions.SpreadsheetId))
        {
            throw new SourceDataException(
                $"Event '{eventOptions.EventId}' requires a non-empty SpreadsheetId.");
        }
    }

    private static void ValidateLifecycleColumnNames(EventSourceOptions eventOptions)
    {
        var lifecycleSettings = new[]
        {
            (Name: eventOptions.CertificateStatusColumn, Setting: nameof(eventOptions.CertificateStatusColumn)),
            (Name: eventOptions.VerificationIdColumn, Setting: nameof(eventOptions.VerificationIdColumn)),
            (Name: eventOptions.ProcessedAtColumn, Setting: nameof(eventOptions.ProcessedAtColumn))
        };

        foreach (var setting in lifecycleSettings)
        {
            if (string.IsNullOrWhiteSpace(setting.Name))
            {
                throw new SourceDataException(
                    $"Event '{eventOptions.EventId}' must configure a non-empty {setting.Setting}.");
            }
        }

        var duplicate = lifecycleSettings
            .GroupBy(setting => NormalizeHeader(setting.Name), StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new SourceDataException(
                $"Event '{eventOptions.EventId}' configures more than one lifecycle field with header " +
                $"'{duplicate.First().Name}'. CertificateStatusColumn, VerificationIdColumn, and " +
                "ProcessedAtColumn must be distinct.");
        }

        var sourceHeaders = new[]
        {
            eventOptions.SourceIdColumn,
            eventOptions.NameColumn,
            eventOptions.EmailColumn,
            eventOptions.PhoneColumn,
            eventOptions.ApprovalColumn,
            eventOptions.ScoreColumn
        }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => NormalizeHeader(name!))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var setting in lifecycleSettings)
        {
            if (sourceHeaders.Contains(NormalizeHeader(setting.Name)))
            {
                throw new SourceDataException(
                    $"Event '{eventOptions.EventId}' configures lifecycle header '{setting.Name}' as both " +
                    "an input and an output column. Use a distinct lifecycle column name to avoid " +
                    "overwriting source data.");
            }
        }
    }

    private static void ValidateUpdates(
        IReadOnlyCollection<SheetCertificateUpdate> updates,
        SheetRangeAddress address,
        EventSourceOptions eventOptions)
    {
        var seenRows = new HashSet<int>();
        foreach (var update in updates)
        {
            if (update.RowNumber <= address.HeaderRowNumber)
            {
                throw new SourceDataException(
                    $"Certificate update row {update.RowNumber} for event '{eventOptions.EventId}' must be " +
                    $"after header row {address.HeaderRowNumber}.");
            }

            if (!seenRows.Add(update.RowNumber))
            {
                throw new SourceDataException(
                    $"Certificate updates for event '{eventOptions.EventId}' contain duplicate row " +
                    $"{update.RowNumber}.");
            }

            if (string.IsNullOrWhiteSpace(update.CertificateStatus))
            {
                throw new SourceDataException(
                    $"Certificate update row {update.RowNumber} for event '{eventOptions.EventId}' " +
                    "requires a non-empty certificate status.");
            }
        }
    }

    private static SourceDataException MissingRequiredValue(
        EventSourceOptions eventOptions,
        int rowNumber,
        string columnName) =>
        new(
            $"Approved row {rowNumber} in sheet '{eventOptions.SheetName}' for event " +
            $"'{eventOptions.EventId}' has no value in required column '{columnName}'.");

    private static ReadOnlyDictionary<string, string> BuildValues(
        IReadOnlyList<string> headers,
        IReadOnlyList<string> row)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            var header = headers[columnIndex].Trim();
            if (header.Length > 0)
            {
                values.TryAdd(header, GetCell(row, columnIndex));
            }
        }

        return new ReadOnlyDictionary<string, string>(values);
    }

    private static string ComputeSourceHash(
        IReadOnlyList<string> headers,
        IReadOnlyList<string> row,
        EventSourceOptions eventOptions)
    {
        var lifecycleHeaders = new[]
        {
            eventOptions.CertificateStatusColumn,
            eventOptions.VerificationIdColumn,
            eventOptions.ProcessedAtColumn
        }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeHeader)
            .ToHashSet(StringComparer.Ordinal);

        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var cells = new List<CanonicalCell>();
        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            var normalizedHeader = NormalizeHeader(headers[columnIndex]);
            if (normalizedHeader.Length == 0 || lifecycleHeaders.Contains(normalizedHeader))
            {
                continue;
            }

            occurrences.TryGetValue(normalizedHeader, out var occurrence);
            occurrence++;
            occurrences[normalizedHeader] = occurrence;
            cells.Add(new CanonicalCell(
                normalizedHeader,
                occurrence,
                NormalizeValue(GetCell(row, columnIndex))));
        }

        var canonical = new StringBuilder("sheet-row-v1\n");
        foreach (var cell in cells
                     .OrderBy(candidate => candidate.Header, StringComparer.Ordinal)
                     .ThenBy(candidate => candidate.Occurrence))
        {
            AppendLengthPrefixed(canonical, cell.Header);
            AppendLengthPrefixed(
                canonical,
                cell.Occurrence.ToString(CultureInfo.InvariantCulture));
            AppendLengthPrefixed(canonical, cell.Value);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void AppendLengthPrefixed(StringBuilder builder, string value)
    {
        builder.Append(Encoding.UTF8.GetByteCount(value));
        builder.Append(':');
        builder.Append(value);
        builder.Append('\n');
    }

    private static string NormalizeValue(string value) =>
        value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Normalize(NormalizationForm.FormC);

    private static bool TryParseScore(string value, out decimal score)
    {
        var normalized = value.Trim();
        if (normalized.EndsWith('%'))
        {
            normalized = normalized[..^1].TrimEnd();
        }

        if (normalized.Contains(',', StringComparison.Ordinal)
            && !normalized.Contains('.', StringComparison.Ordinal))
        {
            var decimalDigits = normalized.Length - normalized.LastIndexOf(',') - 1;
            if (decimalDigits is 1 or 2)
            {
                var decimalCandidate = normalized.Replace(',', '.');
                if (decimal.TryParse(
                        decimalCandidate,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out score))
                {
                    return true;
                }
            }
        }

        if (decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out score))
        {
            return true;
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.CurrentCulture,
            out score);
    }

    private static string? NullIfWhiteSpace(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string GetCell(IReadOnlyList<string> row, int index) =>
        index < row.Count ? row[index] : string.Empty;

    private static string NormalizeHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormC).Trim();
        var builder = new StringBuilder(normalized.Length);
        var previousWasWhitespace = false;
        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }
            }
            else
            {
                builder.Append(char.ToUpperInvariant(character));
                previousWasWhitespace = false;
            }
        }

        return builder.ToString();
    }

    private sealed class HeaderMap
    {
        private readonly IReadOnlyList<string> _headers;
        private readonly int _startColumnNumber;
        private readonly Dictionary<string, List<int>> _indices = new(StringComparer.Ordinal);

        public HeaderMap(IReadOnlyList<string> headers, int startColumnNumber)
        {
            _headers = headers;
            _startColumnNumber = startColumnNumber;
            for (var index = 0; index < headers.Count; index++)
            {
                var normalized = NormalizeHeader(headers[index]);
                if (normalized.Length == 0)
                {
                    continue;
                }

                if (!_indices.TryGetValue(normalized, out var matches))
                {
                    matches = [];
                    _indices.Add(normalized, matches);
                }

                matches.Add(index);
            }
        }

        public int Require(
            string configuredName,
            string settingName,
            EventSourceOptions eventOptions) =>
            Find(configuredName, settingName, eventOptions, required: true)!.Value;

        public int? FindOptional(
            string? configuredName,
            string settingName,
            EventSourceOptions eventOptions) =>
            Find(configuredName, settingName, eventOptions, required: false);

        private int? Find(
            string? configuredName,
            string settingName,
            EventSourceOptions eventOptions,
            bool required)
        {
            if (string.IsNullOrWhiteSpace(configuredName))
            {
                if (!required)
                {
                    return null;
                }

                throw new SourceDataException(
                    $"Event '{eventOptions.EventId}' must configure a non-empty {settingName}.");
            }

            if (!_indices.TryGetValue(NormalizeHeader(configuredName), out var matches))
            {
                if (!required)
                {
                    return null;
                }

                throw new SourceDataException(
                    $"Sheet '{eventOptions.SheetName}' for event '{eventOptions.EventId}' is missing " +
                    $"required column '{configuredName}' configured by {settingName}. Available headers: " +
                    $"{FormatAvailableHeaders()}.");
            }

            if (matches.Count > 1)
            {
                var columns = string.Join(
                    ", ",
                    matches.Select(index => SheetRangeAddress.ColumnName(_startColumnNumber + index)));
                throw new SourceDataException(
                    $"Sheet '{eventOptions.SheetName}' for event '{eventOptions.EventId}' contains " +
                    $"duplicate header '{configuredName}' in columns {columns}. Rename the duplicate " +
                    "header so the configured column is unambiguous.");
            }

            return matches[0];
        }

        private string FormatAvailableHeaders()
        {
            var available = _headers
                .Where(header => !string.IsNullOrWhiteSpace(header))
                .Select(header => $"'{header.Trim()}'")
                .ToArray();
            return available.Length == 0 ? "(none)" : string.Join(", ", available);
        }
    }

    private readonly record struct SheetRangeAddress(
        string RequestRange,
        string SheetName,
        int StartColumnNumber,
        int HeaderRowNumber)
    {
        public static SheetRangeAddress Parse(EventSourceOptions eventOptions)
        {
            if (string.IsNullOrWhiteSpace(eventOptions.SheetName))
            {
                throw new SourceDataException(
                    $"Event '{eventOptions.EventId}' requires a non-empty SheetName.");
            }

            if (string.IsNullOrWhiteSpace(eventOptions.Range))
            {
                throw new SourceDataException(
                    $"Event '{eventOptions.EventId}' requires a non-empty A1 sheet Range.");
            }

            var a1Range = eventOptions.Range.Trim();
            var separator = a1Range.LastIndexOf('!');
            if (separator >= 0)
            {
                var embeddedSheetName = UnquoteSheetName(a1Range[..separator].Trim());
                if (!string.Equals(
                        embeddedSheetName,
                        eventOptions.SheetName,
                        StringComparison.Ordinal))
                {
                    throw new SourceDataException(
                        $"Event '{eventOptions.EventId}' configures SheetName '{eventOptions.SheetName}' " +
                        $"but Range targets sheet '{embeddedSheetName}'. Make both settings target the " +
                        "same sheet.");
                }

                a1Range = a1Range[(separator + 1)..].Trim();
            }

            var startReference = a1Range.Split(':', 2)[0].Trim().Replace("$", string.Empty, StringComparison.Ordinal);
            if (!TryParseStartReference(startReference, out var startColumn, out var startRow))
            {
                throw new SourceDataException(
                    $"Event '{eventOptions.EventId}' has unsupported sheet Range '{eventOptions.Range}'. " +
                    "Configure an A1 range such as 'A:ZZ' or 'A1:ZZ1000'.");
            }

            var escapedSheetName = eventOptions.SheetName.Replace("'", "''", StringComparison.Ordinal);
            return new SheetRangeAddress(
                $"'{escapedSheetName}'!{a1Range}",
                eventOptions.SheetName,
                startColumn,
                startRow);
        }

        public string CellRange(int relativeColumn, int rowNumber)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(relativeColumn);
            ArgumentOutOfRangeException.ThrowIfLessThan(rowNumber, 1);

            var escapedSheetName = SheetName.Replace("'", "''", StringComparison.Ordinal);
            return $"'{escapedSheetName}'!{ColumnName(StartColumnNumber + relativeColumn)}{rowNumber}";
        }

        public static string ColumnName(int columnNumber)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(columnNumber, 1);

            var name = new StringBuilder();
            var remaining = columnNumber;
            while (remaining > 0)
            {
                remaining--;
                name.Insert(0, (char)('A' + (remaining % 26)));
                remaining /= 26;
            }

            return name.ToString();
        }

        private static bool TryParseStartReference(
            string reference,
            out int columnNumber,
            out int rowNumber)
        {
            columnNumber = 1;
            rowNumber = 1;
            if (reference.Length == 0)
            {
                return false;
            }

            var index = 0;
            var parsedColumn = 0;
            while (index < reference.Length && char.IsAsciiLetter(reference[index]))
            {
                try
                {
                    parsedColumn = checked(
                        (parsedColumn * 26)
                        + (char.ToUpperInvariant(reference[index]) - 'A' + 1));
                }
                catch (OverflowException)
                {
                    return false;
                }

                index++;
            }

            var rowStart = index;
            while (index < reference.Length && char.IsAsciiDigit(reference[index]))
            {
                index++;
            }

            if (index != reference.Length || (parsedColumn == 0 && rowStart == index))
            {
                return false;
            }

            if (parsedColumn > 0)
            {
                columnNumber = parsedColumn;
            }

            if (rowStart < index
                && (!int.TryParse(
                        reference.AsSpan(rowStart, index - rowStart),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out rowNumber)
                    || rowNumber < 1))
            {
                return false;
            }

            return true;
        }

        private static string UnquoteSheetName(string value)
        {
            if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            {
                return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
            }

            return value;
        }
    }

    private sealed record CanonicalCell(string Header, int Occurrence, string Value);
}
