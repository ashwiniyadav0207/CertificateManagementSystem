using CertificateEngine.Api;
using CertificateEngine.Configuration;
using CertificateEngine.Domain;
using CertificateEngine.Infrastructure.Audit;
using Xunit;

namespace CertificateEngine.Tests;

public sealed class ManagementLandingTests
{
    [Fact]
    public void DashboardRendersManagementActionsAndEncodesCertificateData()
    {
        var certificate = new CertificateRecord(
            1, "event:source", "0123456789abcdef", "CERT-2026-ABC", "event", "<event>", "source", "hash",
            "<script>alert(1)</script>", null, null, "default", 0, CertificateStatus.Issued,
            "certificates/test.pdf", "hash", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);

        var html = ManagementLanding.Page(
            [certificate], [], true, "http://localhost:5001", "Issued", new AuditVerificationResult(true, 2, "head", null));

        Assert.Contains("Issue a test certificate", html, StringComparison.Ordinal);
        Assert.Contains("/manage/certificates/0123456789abcdef/revoke", html, StringComparison.Ordinal);
        Assert.Contains("Audit passed.", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardShowsProductionPollingWhenEventsAreConfigured()
    {
        var html = ManagementLanding.Page(
            [], [new EventSourceOptions { EventId = "spring", EventName = "Spring event", SpreadsheetId = "sheet" }],
            false, "https://verify.example.org");

        Assert.Contains("/manage/events/spring/poll", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Issue a test certificate", html, StringComparison.Ordinal);
    }
}
