using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CertificateEngine.Configuration;
using CertificateEngine.Domain;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Options;
using SheetsRequest = Google.Apis.Sheets.v4.Data.Request;

namespace CertificateEngine.Infrastructure.Sheets;

public sealed class GoogleSheetsClient : IGoogleSheetsClient, IDisposable
{
    private readonly GoogleSheetsOptions _options;
    private SheetsService? _service;

    public GoogleSheetsClient(IOptions<GoogleSheetsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public async Task<SheetValueTable> ReadValuesAsync(
        string spreadsheetId,
        string range,
        CancellationToken cancellationToken)
    {
        var request = Service.Spreadsheets.Values.Get(spreadsheetId, range);
        request.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
        request.DateTimeRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.DateTimeRenderOptionEnum.FORMATTEDSTRING;

        var response = await request.ExecuteAsync(cancellationToken);
        if (response.Values is null)
        {
            return new SheetValueTable([]);
        }

        var rows = response.Values
            .Select(row => (IReadOnlyList<string>)row.Select(FormatCellValue).ToArray())
            .ToArray();
        return new SheetValueTable(rows);
    }

    public async Task EnsureColumnCapacityAsync(
        string spreadsheetId,
        string sheetName,
        int requiredColumnCount,
        CancellationToken cancellationToken)
    {
        if (requiredColumnCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredColumnCount),
                requiredColumnCount,
                "The required column count must be positive.");
        }

        var getRequest = Service.Spreadsheets.Get(spreadsheetId);
        getRequest.Fields = "sheets(properties(gridProperties(columnCount),sheetId,title))";
        var spreadsheet = await getRequest.ExecuteAsync(cancellationToken);
        var sheet = spreadsheet.Sheets?.SingleOrDefault(
            candidate => string.Equals(candidate.Properties?.Title, sheetName, StringComparison.Ordinal));
        if (sheet?.Properties is null)
        {
            throw new SourceDataException(
                $"Spreadsheet '{spreadsheetId}' does not contain a sheet named '{sheetName}'. " +
                "Check the event's SheetName setting and service-account access.");
        }

        var currentColumnCount = sheet.Properties.GridProperties?.ColumnCount ?? 0;
        if (currentColumnCount >= requiredColumnCount)
        {
            return;
        }

        if (!sheet.Properties.SheetId.HasValue)
        {
            throw new SourceDataException(
                $"Google Sheets did not return an ID for sheet '{sheetName}' in spreadsheet '{spreadsheetId}'.");
        }

        var body = new BatchUpdateSpreadsheetRequest
        {
            Requests =
            [
                new SheetsRequest
                {
                    AppendDimension = new AppendDimensionRequest
                    {
                        Dimension = "COLUMNS",
                        Length = requiredColumnCount - currentColumnCount,
                        SheetId = sheet.Properties.SheetId.Value
                    }
                }
            ]
        };
        await Service.Spreadsheets.BatchUpdate(body, spreadsheetId).ExecuteAsync(cancellationToken);
    }

    public async Task BatchWriteValuesAsync(
        string spreadsheetId,
        IReadOnlyCollection<SheetCellWrite> writes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writes);
        if (writes.Count == 0)
        {
            return;
        }

        var body = new BatchUpdateValuesRequest
        {
            ValueInputOption = "RAW",
            Data = writes
                .Select(write => new ValueRange
                {
                    MajorDimension = "ROWS",
                    Range = write.Range,
                    Values = [new List<object> { write.Value }]
                })
                .ToList()
        };

        await Service.Spreadsheets.Values.BatchUpdate(body, spreadsheetId).ExecuteAsync(cancellationToken);
    }

    public void Dispose() => _service?.Dispose();

    private SheetsService Service => _service ??= CreateService(_options);

    private static SheetsService CreateService(GoogleSheetsOptions settings)
    {
        var credential = CreateCredential(settings);
        return new SheetsService(new BaseClientService.Initializer
        {
            ApplicationName = string.IsNullOrWhiteSpace(settings.ApplicationName) ? "CertificateEngine" : settings.ApplicationName.Trim(),
            HttpClientInitializer = credential
        });
    }

    private static ServiceAccountCredential CreateCredential(GoogleSheetsOptions options)
    {
        var json = LoadServiceAccountJson(options);
        ServiceAccountKey key;
        try
        {
            key = JsonSerializer.Deserialize<ServiceAccountKey>(json)
                ?? throw new InvalidOperationException("The Google Sheets service-account JSON is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The configured Google Sheets credential is not valid service-account JSON.",
                exception);
        }

        if (!string.Equals(key.Type, "service_account", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The configured Google Sheets credential must have type 'service_account'.");
        }

        if (string.IsNullOrWhiteSpace(key.ClientEmail) || string.IsNullOrWhiteSpace(key.PrivateKey))
        {
            throw new InvalidOperationException(
                "The Google Sheets service-account JSON must contain client_email and private_key.");
        }

        var initializer = new ServiceAccountCredential.Initializer(key.ClientEmail)
        {
            KeyId = key.PrivateKeyId,
            ProjectId = key.ProjectId,
            Scopes = [SheetsService.Scope.Spreadsheets]
        };
        if (!string.IsNullOrWhiteSpace(key.TokenUri)
            && (!Uri.TryCreate(key.TokenUri, UriKind.Absolute, out var tokenUri)
                || tokenUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "The token_uri in the Google Sheets service-account JSON must be an absolute HTTPS URL.");
        }

        return new ServiceAccountCredential(initializer.FromPrivateKey(key.PrivateKey));
    }

    private static string LoadServiceAccountJson(GoogleSheetsOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ServiceAccountJson))
        {
            return options.ServiceAccountJson;
        }

        if (!string.IsNullOrWhiteSpace(options.ServiceAccountJsonEnvironmentVariable))
        {
            var environmentJson = Environment.GetEnvironmentVariable(
                options.ServiceAccountJsonEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(environmentJson))
            {
                return environmentJson;
            }
        }

        var path = options.ServiceAccountJsonPath;
        if (string.IsNullOrWhiteSpace(path)
            && !string.IsNullOrWhiteSpace(options.ServiceAccountPathEnvironmentVariable))
        {
            path = Environment.GetEnvironmentVariable(options.ServiceAccountPathEnvironmentVariable);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "Google Sheets service-account credentials are not configured. Set " +
                "GoogleSheets:ServiceAccountJson, its configured environment variable, " +
                "GoogleSheets:ServiceAccountJsonPath, or GOOGLE_APPLICATION_CREDENTIALS.");
        }

        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, AppContext.BaseDirectory);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"The configured Google Sheets service-account file does not exist: '{fullPath}'.");
        }

        return File.ReadAllText(fullPath);
    }

    private static string FormatCellValue(object? value) => value switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private sealed class ServiceAccountKey
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("project_id")]
        public string? ProjectId { get; init; }

        [JsonPropertyName("private_key_id")]
        public string? PrivateKeyId { get; init; }

        [JsonPropertyName("private_key")]
        public string? PrivateKey { get; init; }

        [JsonPropertyName("client_email")]
        public string? ClientEmail { get; init; }

        [JsonPropertyName("token_uri")]
        public string? TokenUri { get; init; }
    }
}
