using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CertificateEngine.Configuration;
using CertificateEngine.Domain;
using CertificateEngine.Infrastructure.Audit;

namespace CertificateEngine.Api;

public static class ManagementLanding
{
    public static string Page(
        IReadOnlyList<CertificateRecord> certificates,
        IReadOnlyList<EventSourceOptions> events,
        bool demoEnabled,
        string publicBaseUrl,
        string? notice = null,
        AuditVerificationResult? audit = null)
    {
        var html = new StringBuilder();
        html.Append("""
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Certificate management</title><style>
:root{--ink:#10263e;--gold:#eab75a;--mist:#f5f7f9;--line:#dbe1e8;--good:#087b59;--bad:#b42318}*{box-sizing:border-box}body{margin:0;background:var(--mist);color:var(--ink);font:15px system-ui,sans-serif}main{max-width:1160px;margin:auto;padding:42px 24px 72px}header{display:flex;justify-content:space-between;gap:20px;align-items:center;margin-bottom:28px}.mark{display:inline-grid;place-items:center;width:46px;height:46px;border-radius:14px;background:var(--ink);color:var(--gold);font-size:22px}h1{font:700 clamp(2rem,5vw,3.7rem)/1 Georgia,serif;margin:10px 0}.tag{margin:0;color:#886526;font-size:.75rem;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.muted{color:#617083}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}.card{padding:23px;background:#fff;border:1px solid var(--line);border-radius:18px;box-shadow:0 12px 28px #10263e0c}.card h2{margin:0 0 7px;font:700 1.4rem Georgia,serif}.card p{margin:0 0 18px;line-height:1.55}.wide{grid-column:1/-1}form{display:flex;flex-wrap:wrap;gap:10px}input{min-width:180px;flex:1;border:1px solid #bdc9d4;border-radius:9px;padding:11px;font:inherit}button,.button{border:0;border-radius:9px;background:var(--ink);color:#fff;padding:11px 14px;font:700 .9rem system-ui;cursor:pointer;text-decoration:none}.secondary{background:#eaf0f4;color:var(--ink)}.danger{background:var(--bad)}.notice{padding:13px 15px;border-radius:10px;background:#e5f6ef;color:#075b42;margin-bottom:20px}.audit-good{color:var(--good)}.audit-bad{color:var(--bad)}table{border-collapse:collapse;width:100%;margin-top:8px}th,td{padding:13px 10px;border-bottom:1px solid var(--line);text-align:left;vertical-align:middle}th{font-size:.72rem;letter-spacing:.06em;text-transform:uppercase;color:#617083}.badge{display:inline-block;border-radius:999px;padding:4px 8px;font-weight:700;font-size:.78rem;background:#eaf0f4}.issued{background:#dff5eb;color:#075b42}.revoked,.failed{background:#ffe6e4;color:#9b1c1c}.actions{display:flex;gap:7px;flex-wrap:wrap}.actions form{display:inline}.small{padding:7px 9px;font-size:.78rem}.empty{padding:22px 0;color:#617083}@media(max-width:760px){main{padding:28px 14px}.grid{grid-template-columns:1fr}.wide{grid-column:auto}table{font-size:.84rem}th:nth-child(3),td:nth-child(3),th:nth-child(5),td:nth-child(5){display:none}}</style></head><body><main><header><div><span class="mark">✓</span><p class="tag">Private local service</p><h1>Certificate management</h1><p class="muted">Issue, inspect, revoke, and audit your certificate records.</p></div><a class="button secondary" href="/">Refresh</a></header>
""");

        if (!string.IsNullOrWhiteSpace(notice)) html.Append("<div class=\"notice\">").Append(Encode(notice)).Append("</div>");
        html.Append("<section class=\"grid\">");
        if (demoEnabled)
        {
            html.Append("""<section class="card"><h2>Issue a test certificate</h2><p>Create a local certificate immediately. This is available only while no production events are configured.</p><form method="post" action="/manage/issue"><input name="fullName" required maxlength="200" placeholder="Participant full name" aria-label="Participant full name"><input name="eventName" maxlength="200" placeholder="Event name (optional)" aria-label="Event name"><button>Issue certificate</button></form></section>""");
        }
        else
        {
            html.Append("<section class=\"card\"><h2>Production events enabled</h2><p>Certificates are issued from approved Google Sheet submissions. Use Poll now to fetch an event immediately.</p><div class=\"actions\">");
            foreach (var eventOptions in events)
            {
                html.Append("<form method=\"post\" action=\"/manage/events/").Append(Uri.EscapeDataString(eventOptions.EventId)).Append("/poll\"><button>").Append(Encode(eventOptions.EventName)).Append(" · Poll now</button></form>");
            }
            html.Append("</div></section>");
        }
        html.Append("<section class=\"card\"><h2>Integrity audit</h2><p>Validate the append-only audit chain across every certificate and delivery event.</p><form method=\"post\" action=\"/manage/audit\"><button class=\"secondary\">Run audit</button></form>");
        if (audit is not null) html.Append("<p class=\"").Append(audit.IsValid ? "audit-good" : "audit-bad").Append("\"><strong>").Append(audit.IsValid ? "Audit passed." : "Audit failed.").Append("</strong> ").Append(Encode(audit.Error ?? $"{audit.EventCount} audit events verified.")).Append("</p>");
        html.Append("</section><section class=\"card wide\"><h2>Certificate records</h2><p>Newest 200 records. Verification links open the public-facing verifier.</p>");
        if (certificates.Count == 0) html.Append("<p class=\"empty\">No certificates have been issued yet.</p>");
        else
        {
            html.Append("<table><thead><tr><th>Holder</th><th>Certificate</th><th>Event</th><th>Status</th><th>Issued</th><th>Actions</th></tr></thead><tbody>");
            foreach (var certificate in certificates)
            {
                var status = certificate.Status.ToString();
                html.Append("<tr><td><strong>").Append(Encode(certificate.ParticipantName)).Append("</strong><br><span class=\"muted\">").Append(Encode(certificate.PublicId[..12])).Append("…</span></td><td>").Append(Encode(certificate.CertificateNumber)).Append("</td><td>").Append(Encode(certificate.EventName)).Append("</td><td><span class=\"badge ").Append(status.ToLowerInvariant()).Append("\">").Append(Encode(status)).Append("</span></td><td>").Append(certificate.IssuedAt?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "—").Append("</td><td><div class=\"actions\"><a class=\"button secondary small\" target=\"_blank\" rel=\"noreferrer\" href=\"").Append(Encode(VerificationUrl(publicBaseUrl, certificate.PublicId))).Append("\">Verify</a>");
                if (!string.IsNullOrWhiteSpace(certificate.ArtifactPath)) html.Append("<a class=\"button secondary small\" href=\"/manage/certificates/").Append(Uri.EscapeDataString(certificate.PublicId)).Append("/download\">PDF</a>");
                if (certificate.Status == CertificateStatus.Issued) html.Append("<form method=\"post\" action=\"/manage/certificates/").Append(Uri.EscapeDataString(certificate.PublicId)).Append("/revoke\" onsubmit=\"return confirm('Revoke this certificate?')\"><input name=\"reason\" required maxlength=\"500\" placeholder=\"Revocation reason\" aria-label=\"Revocation reason\"><button class=\"danger small\">Revoke</button></form>");
                html.Append("</div></td></tr>");
            }
            html.Append("</tbody></table>");
        }
        html.Append("</section></section></main></body></html>");
        return html.ToString();
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);

    private static string VerificationUrl(string baseUrl, string publicId) =>
        $"{baseUrl.TrimEnd('/')}/verify/{Uri.EscapeDataString(publicId)}";
}
