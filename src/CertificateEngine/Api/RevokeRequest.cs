namespace CertificateEngine.Api;

public sealed record RevokeRequest(string Reason);

public sealed record DemoIssueRequest(string FullName, string? EventName);
