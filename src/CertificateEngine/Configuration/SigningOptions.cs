namespace CertificateEngine.Configuration;

public sealed class SigningOptions
{
    public const string SectionName = "Signing";

    public bool Enabled { get; init; }
    public string PfxPath { get; init; } = string.Empty;
    public string PfxPassword { get; init; } = string.Empty;
    public string TrustedRootPath { get; init; } = string.Empty;
    public string? TimestampServerUrl { get; init; }
    public string Reason { get; init; } = "Official certificate issued by the organization";
    public string Location { get; init; } = string.Empty;
    public string ContactInfo { get; init; } = string.Empty;
}
