using CertificateEngine.Configuration;
using CertificateEngine.Domain;

namespace CertificateEngine.Infrastructure.Sheets;

public interface ISheetSource
{
    Task<IReadOnlyList<SheetSubmission>> ReadApprovedSubmissionsAsync(
        EventSourceOptions eventOptions,
        CancellationToken cancellationToken);

    Task WriteCertificateUpdatesAsync(
        EventSourceOptions eventOptions,
        IReadOnlyCollection<SheetCertificateUpdate> updates,
        CancellationToken cancellationToken);
}

public sealed record SheetCertificateUpdate(
    int RowNumber,
    string CertificateStatus,
    string? VerificationId,
    DateTimeOffset ProcessedAt);

public interface IGoogleSheetsClient
{
    Task<SheetValueTable> ReadValuesAsync(
        string spreadsheetId,
        string range,
        CancellationToken cancellationToken);

    Task EnsureColumnCapacityAsync(
        string spreadsheetId,
        string sheetName,
        int requiredColumnCount,
        CancellationToken cancellationToken);

    Task BatchWriteValuesAsync(
        string spreadsheetId,
        IReadOnlyCollection<SheetCellWrite> writes,
        CancellationToken cancellationToken);
}

public sealed record SheetValueTable(IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record SheetCellWrite(string Range, string Value);
