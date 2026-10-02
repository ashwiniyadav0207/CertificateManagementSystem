using System.Text.Encodings.Web;
using System.Globalization;
using CertificateVerification.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.Configure<VerificationOptions>(builder.Configuration.GetSection(VerificationOptions.SectionName));
builder.Services.AddSingleton<CertificateLookup>();
var app = builder.Build();
app.UseCors();

app.MapGet("/favicon.ico", () => Results.Content("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'><rect width='64' height='64' rx='16' fill='#112a46'/><path d='M32 12l16 7v12c0 11-6 18-16 22-10-4-16-11-16-22V19l16-7z' fill='#f5c66a'/><path d='M24 32l5 5 11-12' fill='none' stroke='#112a46' stroke-width='4' stroke-linecap='round'/></svg>", "image/svg+xml"));
app.MapGet("/", () => Results.Redirect("http://localhost:3000/verify"));
app.MapGet("/verify/{publicId}", (string publicId) => Results.Redirect($"http://localhost:3000/verify/{Uri.EscapeDataString(publicId)}"));
app.MapGet("/api/verify/{publicId}", async (string publicId, CertificateLookup lookup, CancellationToken ct) =>
{
    var certificate = await lookup.FindAsync(publicId, ct);
    return certificate is null ? Results.NotFound(new { publicId, status = "NotFound" }) : Results.Ok(new { publicId = certificate.Record.PublicId, status = certificate.Status, certificateNumber = certificate.Record.CertificateNumber, participantName = certificate.Record.ParticipantName, eventName = certificate.Record.EventName, issuedAtUtc = certificate.Record.IssuedAt, revokedAtUtc = certificate.Record.RevokedAt, revocationReason = certificate.Record.RevocationReason, signatureValid = certificate.SignatureValid, fileSha256 = certificate.Record.ArtifactSha256 });
});
await app.RunAsync();
