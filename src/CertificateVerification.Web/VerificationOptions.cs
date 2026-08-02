namespace CertificateVerification.Web;

public sealed class VerificationOptions
{
    public const string SectionName = "Verification";
    public string DataDirectory { get; init; } = "data";
    public string TrustedRootPath { get; init; } = string.Empty;
}
