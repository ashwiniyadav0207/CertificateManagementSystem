namespace CertificateEngine.Configuration;

public sealed class GoogleSheetsOptions
{
    public const string SectionName = "GoogleSheets";

    public string ApplicationName { get; init; } = "CertificateEngine";
    public string? ServiceAccountJson { get; init; }
    public string ServiceAccountJsonEnvironmentVariable { get; init; } = "GOOGLE_SERVICE_ACCOUNT_JSON";
    public string? ServiceAccountJsonPath { get; init; }
    public string ServiceAccountPathEnvironmentVariable { get; init; } = "GOOGLE_APPLICATION_CREDENTIALS";
}
