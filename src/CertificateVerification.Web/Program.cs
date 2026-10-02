using System.Text.Encodings.Web;
using System.Globalization;
using CertificateVerification.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<VerificationOptions>(builder.Configuration.GetSection(VerificationOptions.SectionName));
builder.Services.AddSingleton<CertificateLookup>();
var app = builder.Build();

app.MapGet("/favicon.ico", () => Results.Content("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'><rect width='64' height='64' rx='16' fill='#112a46'/><path d='M32 12l16 7v12c0 11-6 18-16 22-10-4-16-11-16-22V19l16-7z' fill='#f5c66a'/><path d='M24 32l5 5 11-12' fill='none' stroke='#112a46' stroke-width='4' stroke-linecap='round'/></svg>", "image/svg+xml"));
app.MapGet("/", () => Results.Content(Page("Certificate verification", "", null), "text/html"));
app.MapGet("/verify/{publicId}", async (string publicId, CertificateLookup lookup, CancellationToken ct) =>
{
    var certificate = await lookup.FindAsync(publicId, ct);
    return Results.Content(Page("Certificate verification", publicId, certificate), "text/html");
});
app.MapGet("/api/verify/{publicId}", async (string publicId, CertificateLookup lookup, CancellationToken ct) =>
{
    var certificate = await lookup.FindAsync(publicId, ct);
    return certificate is null ? Results.NotFound(new { publicId, status = "NotFound" }) : Results.Ok(new { publicId = certificate.Record.PublicId, status = certificate.Status, certificateNumber = certificate.Record.CertificateNumber, participantName = certificate.Record.ParticipantName, eventName = certificate.Record.EventName, issuedAtUtc = certificate.Record.IssuedAt, revokedAtUtc = certificate.Record.RevokedAt, revocationReason = certificate.Record.RevocationReason, signatureValid = certificate.SignatureValid, fileSha256 = certificate.Record.ArtifactSha256 });
});
await app.RunAsync();

static string Page(string title, string id, VerifiedCertificate? certificate)
{
    var encodedId = HtmlEncoder.Default.Encode(id);
    var card = certificate is null && id.Length > 0
        ? "<section class='result error'><span class='icon'>!</span><div><p class='eyebrow'>Certificate not found</p><h2>We could not find that record.</h2><p>Check the certificate ID or scan the QR code again.</p></div></section>"
        : certificate is null ? "<section class='search-card'><p class='eyebrow'>Official Verification Portal</p><h2>Verify a credential</h2><p>Enter the certificate ID printed on the document or scan its QR code.</p></section>"
        : CertificateCard(certificate);
    return $$$"""
<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{{{title}}}</title>
<style>
@import url('https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;500;600;700&family=Playfair+Display:wght@600;700&display=swap');
:root{--ink:#10263e;--muted:#617083;--gold:#eebd59;--paper:#fffdf8;--line:#e7dfd0;--good:#047857;--bad:#b42318}*{box-sizing:border-box}body{margin:0;min-height:100vh;color:var(--ink);font-family:'DM Sans',system-ui,sans-serif;background:radial-gradient(circle at 15% 10%,#fff3d7 0,transparent 27rem),radial-gradient(circle at 86% 18%,#dbeef2 0,transparent 24rem),#f8f7f3}.shell{max-width:980px;margin:auto;padding:28px 24px 70px}.brand{display:flex;align-items:center;gap:13px;font-weight:700;letter-spacing:.01em}.mark{width:42px;height:42px;border-radius:13px;display:grid;place-items:center;background:var(--ink);color:var(--gold);font-size:22px;box-shadow:0 10px 25px #10263e30}.hero{text-align:center;padding:72px 0 32px}.eyebrow{text-transform:uppercase;letter-spacing:.15em;font-size:.72rem;font-weight:700;color:#8a6a27;margin:0 0 12px}.hero h1{font-family:'Playfair Display',serif;font-size:clamp(2.4rem,6vw,4.4rem);line-height:1.05;margin:0 auto;max-width:760px}.hero p{color:var(--muted);font-size:1.08rem;max-width:530px;margin:18px auto 0}.search{display:flex;gap:10px;max-width:650px;margin:32px auto 0;padding:7px;background:#fff;border:1px solid var(--line);border-radius:16px;box-shadow:0 18px 40px #2c3c4b15}.search input{border:0;outline:0;min-width:0;flex:1;padding:13px 15px;font:inherit;color:var(--ink)}button{border:0;border-radius:11px;background:var(--ink);color:white;padding:13px 20px;font:600 .92rem inherit;cursor:pointer}.search-card,.result{max-width:760px;margin:20px auto;background:#fffdfa;border:1px solid var(--line);padding:34px;border-radius:22px;box-shadow:0 22px 45px #2c3c4b10}.search-card h2,.result h2{font-family:'Playfair Display',serif;font-size:2rem;margin:0 0 10px}.search-card p:last-child,.result p{color:var(--muted);line-height:1.65}.result{display:flex;gap:22px;align-items:flex-start}.icon{width:50px;height:50px;flex:none;display:grid;place-items:center;border-radius:50%;font-size:24px;font-weight:700}.valid .icon{background:#d9f7ec;color:var(--good)}.error .icon,.invalid .icon{background:#ffe5e2;color:var(--bad)}.details{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:17px;border-top:1px solid var(--line);margin-top:20px;padding-top:20px}.details span{display:block;color:var(--muted);font-size:.78rem;margin-bottom:4px}.details b{font-size:.97rem}.foot{margin-top:65px;text-align:center;color:var(--muted);font-size:.86rem}@media(max-width:600px){.hero{padding-top:52px}.result{padding:25px;gap:14px}.details{grid-template-columns:1fr}.search{border-radius:14px}.search button{padding:13px}}</style></head>
<body><main class='shell'><div class='brand'><span class='mark'>✓</span><span>Official Credential Verification</span></div><section class='hero'><p class='eyebrow'>Public Integrity Verification</p><h1>Verify official credentials.</h1><p>Every certificate is cryptographically signed and independently verifiable against the issuing authority&rsquo;s root key.</p><form class='search' onsubmit="event.preventDefault();const id=this.querySelector('input').value.trim();if(id)location='/verify/'+encodeURIComponent(id)"><input aria-label='Certificate ID' value='{{{encodedId}}}' placeholder='Enter certificate ID (e.g. 55c2710b...)'><button>Verify credential</button></form></section>{{{card}}}<p class='foot'>This independent verification portal evaluates cryptographic CMS digital signatures and tamper-evident SHA-256 artifacts.</p></main></body></html>
""";
}

static string CertificateCard(VerifiedCertificate certificate)
{
    var valid = certificate.Status == "Valid";
    var state = valid ? "valid" : "invalid";
    var heading = valid ? "This certificate is authentic." : certificate.Status == "Revoked" ? "This certificate has been revoked." : "This certificate could not be validated.";
    var icon = valid ? "✓" : "!";
    var r = certificate.Record;
    var reasonField = !string.IsNullOrWhiteSpace(r.RevocationReason) ? $"<div><span>Revocation reason</span><b style='color:var(--bad);'>{HtmlEncoder.Default.Encode(r.RevocationReason)}</b></div>" : "";
    var revokedAtField = r.RevokedAt.HasValue ? $"<div><span>Revoked date</span><b>{r.RevokedAt.Value.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)}</b></div>" : "";
    return $"<section class='result {state}'><span class='icon'>{icon}</span><div style='flex:1;'><p class='eyebrow'>{HtmlEncoder.Default.Encode(certificate.Status)}</p><h2>{heading}</h2><p>The record below was checked against the signed PDF artifact and the offline Root CA authority.</p><div class='details'><div><span>Recipient name</span><b>{HtmlEncoder.Default.Encode(r.ParticipantName)}</b></div><div><span>Event / Cohort</span><b>{HtmlEncoder.Default.Encode(r.EventName)}</b></div><div><span>Certificate number</span><b>{HtmlEncoder.Default.Encode(r.CertificateNumber)}</b></div><div><span>Issued date</span><b>{(r.IssuedAt?.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture) ?? "—")}</b></div>{revokedAtField}{reasonField}</div></div></section>";
}
