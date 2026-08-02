namespace CertificateEngine.Domain;

public sealed record Participant(
    string SourceId,
    string FullName,
    string? Email,
    string? PhoneNumber);

public sealed record SheetSubmission(
    string EventId,
    string EventName,
    string SpreadsheetId,
    string SheetName,
    int RowNumber,
    Participant Participant,
    string SourceHash,
    decimal? Score,
    IReadOnlyDictionary<string, string> Values);

public enum CertificateStatus
{
    Pending,
    Issued,
    Revoked,
    Failed
}

public sealed record CertificateRecord(
    long Id,
    string IssuanceKey,
    string PublicId,
    string CertificateNumber,
    string EventId,
    string EventName,
    string SourceId,
    string SourceHash,
    string ParticipantName,
    string? ParticipantEmail,
    string? ParticipantPhone,
    string TemplateId,
    int SourceRowNumber,
    CertificateStatus Status,
    string? ArtifactPath,
    string? ArtifactSha256,
    string? SignerThumbprint,
    DateTimeOffset CreatedAt,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? RevokedAt,
    string? RevocationReason);

public enum DeliveryChannel
{
    Email,
    WhatsApp
}

public enum DeliveryStatus
{
    Pending,
    Processing,
    Sent,
    Failed
}

public sealed record DeliveryRecord(
    long Id,
    long CertificateId,
    DeliveryChannel Channel,
    string Destination,
    DeliveryStatus Status,
    int AttemptCount,
    DateTimeOffset NextAttemptAt,
    string? ProviderMessageId,
    string? LastError);

public sealed record SendResult(bool Success, string? ProviderMessageId, string? Error, bool IsTransient)
{
    public static SendResult Sent(string providerMessageId) => new(true, providerMessageId, null, false);
    public static SendResult TransientFailure(string error) => new(false, null, error, true);
    public static SendResult PermanentFailure(string error) => new(false, null, error, false);
}
