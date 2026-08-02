using CertificateEngine.Api;
using CertificateEngine.Domain;
using CertificateEngine.Certificates;
using CertificateEngine.Configuration;
using CertificateEngine.Infrastructure.Delivery;
using CertificateEngine.Infrastructure.Audit;
using CertificateEngine.Infrastructure.Persistence;
using CertificateEngine.Infrastructure.Sheets;
using Microsoft.Extensions.Options;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOptions<PlatformOptions>().Bind(builder.Configuration.GetSection(PlatformOptions.SectionName)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<PlatformOptions>, PlatformOptionsValidator>();
builder.Services.AddOptions<SigningOptions>().Bind(builder.Configuration.GetSection(SigningOptions.SectionName)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SigningOptions>, SigningOptionsValidator>();
builder.Services.Configure<GoogleSheetsOptions>(builder.Configuration.GetSection(GoogleSheetsOptions.SectionName));
builder.Services.AddSingleton<CertificateDatabase>();
builder.Services.AddSingleton<CertificateRepository>();
builder.Services.AddSingleton<CertificateArtifactService>();
builder.Services.AddSingleton<IGoogleSheetsClient, GoogleSheetsClient>();
builder.Services.AddSingleton<ISheetSource, GoogleSheetsSource>();
builder.Services.AddSingleton<CertificateIssuanceWorker>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<CertificateIssuanceWorker>());
builder.Services.AddCertificateDelivery(builder.Configuration);

var app = builder.Build();
await app.Services.GetRequiredService<CertificateDatabase>().InitializeAsync(CancellationToken.None);

bool Authorized(HttpRequest request, IOptions<PlatformOptions> options) => request.Headers.TryGetValue("X-Api-Key", out var key) && key.Count == 1 && string.Equals(key[0], options.Value.InternalApiKey, StringComparison.Ordinal);
bool ManagementAuthorized(HttpContext context, IOptions<PlatformOptions> options) =>
    context.Connection.RemoteIpAddress is not null && IPAddress.IsLoopback(context.Connection.RemoteIpAddress)
    || Authorized(context.Request, options);

async Task<IResult> ManagementPageAsync(HttpContext context, IOptions<PlatformOptions> options, CertificateRepository repository, string? notice, AuditVerificationResult? audit, CancellationToken ct)
{
    if (!ManagementAuthorized(context, options)) return Results.Unauthorized();
    var platform = options.Value;
    var certificates = await repository.ListAsync(200, ct);
    return Results.Content(ManagementLanding.Page(certificates, platform.Events, platform.Events.Count == 0, platform.PublicBaseUrl, notice, audit), "text/html");
}

app.MapGet("/", (HttpContext context, string? notice, IOptions<PlatformOptions> options, CertificateRepository repository, CancellationToken ct) =>
    ManagementPageAsync(context, options, repository, notice, null, ct));
app.MapGet("/favicon.ico", () => Results.NoContent());
app.MapGet("/verify/{**path}", () => Results.NotFound());

app.MapPost("/manage/issue", async (HttpContext context, IOptions<PlatformOptions> options, CertificateRepository repository, CertificateArtifactService artifacts, CancellationToken ct) =>
{
    if (!ManagementAuthorized(context, options)) return Results.Unauthorized();
    if (options.Value.Events.Count != 0) return Results.BadRequest("Demo issuance is disabled while production events are configured.");
    var form = await context.Request.ReadFormAsync(ct);
    var fullName = form["fullName"].ToString().Trim();
    var eventName = form["eventName"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(fullName)) return Results.BadRequest("Participant full name is required.");
    var eventOptions = new EventSourceOptions { EventId = "local-demo", EventName = string.IsNullOrWhiteSpace(eventName) ? "Local testing event" : eventName, TemplateId = "default" };
    var sourceId = Guid.NewGuid().ToString("N");
    var submission = new SheetSubmission(eventOptions.EventId, eventOptions.EventName, string.Empty, string.Empty, 0,
        new Participant(sourceId, fullName, null, null), sourceId, null, new Dictionary<string, string>());
    var (certificate, _) = await repository.GetOrCreateAsync(submission, eventOptions, ct);
    var artifact = await artifacts.CreateAsync(certificate, ct);
    await repository.MarkIssuedAsync(certificate.Id, artifact.ArtifactPath, artifact.Sha256, artifact.SignerThumbprint, eventOptions, ct);
    return Results.Redirect($"/?notice={Uri.EscapeDataString($"Issued {certificate.CertificateNumber} for {certificate.ParticipantName}.")}");
});

app.MapPost("/manage/certificates/{publicId}/revoke", async (string publicId, HttpContext context, IOptions<PlatformOptions> options, CertificateRepository repository, CancellationToken ct) =>
{
    if (!ManagementAuthorized(context, options)) return Results.Unauthorized();
    var form = await context.Request.ReadFormAsync(ct);
    var reason = form["reason"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(reason)) return Results.BadRequest("A revocation reason is required.");
    var revoked = await repository.RevokeAsync(publicId, reason, ct);
    return Results.Redirect($"/?notice={Uri.EscapeDataString(revoked ? "Certificate revoked." : "Certificate could not be revoked.")}");
});

app.MapPost("/manage/events/{eventId}/poll", async (string eventId, HttpContext context, IOptions<PlatformOptions> options, CertificateIssuanceWorker worker, CancellationToken ct) =>
{
    if (!ManagementAuthorized(context, options)) return Results.Unauthorized();
    var eventOptions = options.Value.Events.FirstOrDefault(e => string.Equals(e.EventId, eventId, StringComparison.OrdinalIgnoreCase));
    if (eventOptions is null) return Results.NotFound();
    await worker.PollEventAsync(eventOptions, ct);
    return Results.Redirect($"/?notice={Uri.EscapeDataString($"Polled {eventOptions.EventName}.")}");
});

app.MapPost("/manage/audit", async (HttpContext context, IOptions<PlatformOptions> options, CertificateRepository repository, CancellationToken ct) =>
    await ManagementPageAsync(context, options, repository, null, await repository.VerifyAuditChainAsync(ct), ct));

app.MapGet("/manage/certificates/{publicId}/download", async (string publicId, HttpContext context, IOptions<PlatformOptions> options, CertificateRepository repository, CertificateDatabase database, CancellationToken ct) =>
{
    if (!ManagementAuthorized(context, options)) return Results.Unauthorized();
    var certificate = await repository.FindByPublicIdAsync(publicId, ct);
    if (certificate?.ArtifactPath is not { Length: > 0 } artifactPath) return Results.NotFound();
    var root = Path.GetFullPath(database.DataDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    var file = Path.GetFullPath(Path.Combine(root, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
    if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(file)) return Results.NotFound();
    return Results.File(file, "application/pdf", $"certificate-{certificate.CertificateNumber}.pdf");
});

app.MapGet("/internal/health", (HttpRequest request, IOptions<PlatformOptions> options) => Authorized(request, options) ? Results.Ok(new { status = "ok" }) : Results.Unauthorized());
app.MapPost("/internal/demo/issue", async (DemoIssueRequest body, HttpRequest request, IOptions<PlatformOptions> options, CertificateRepository repository, CertificateArtifactService artifacts, CancellationToken ct) =>
{
    if (!Authorized(request, options)) return Results.Unauthorized();
    if (options.Value.Events.Count != 0) return Results.BadRequest(new { error = "Demo issuance is only available when no production events are configured." });
    if (string.IsNullOrWhiteSpace(body.FullName)) return Results.BadRequest(new { error = "FullName is required." });
    var eventOptions = new EventSourceOptions { EventId = "local-demo", EventName = string.IsNullOrWhiteSpace(body.EventName) ? "Local testing event" : body.EventName.Trim(), TemplateId = "default" };
    var sourceId = Guid.NewGuid().ToString("N");
    var submission = new SheetSubmission(eventOptions.EventId, eventOptions.EventName, string.Empty, string.Empty, 0,
        new Participant(sourceId, body.FullName.Trim(), null, null), sourceId, null, new Dictionary<string, string>());
    var (certificate, _) = await repository.GetOrCreateAsync(submission, eventOptions, ct);
    var artifact = await artifacts.CreateAsync(certificate, ct);
    await repository.MarkIssuedAsync(certificate.Id, artifact.ArtifactPath, artifact.Sha256, artifact.SignerThumbprint, eventOptions, ct);
    return Results.Created($"/verify/{certificate.PublicId}", new { certificate.PublicId, certificate.CertificateNumber, verifyPath = $"/verify/{certificate.PublicId}" });
});
app.MapPost("/internal/events/{eventId}/poll-now", async (string eventId, HttpRequest request, IOptions<PlatformOptions> options, CertificateIssuanceWorker worker, CancellationToken ct) =>
{
    if (!Authorized(request, options)) return Results.Unauthorized();
    var eventOptions = options.Value.Events.FirstOrDefault(e => string.Equals(e.EventId, eventId, StringComparison.OrdinalIgnoreCase));
    if (eventOptions is null) return Results.NotFound();
    await worker.PollEventAsync(eventOptions, ct); return Results.Accepted();
});
app.MapPost("/internal/certificates/{publicId}/revoke", async (string publicId, RevokeRequest body, HttpRequest request, IOptions<PlatformOptions> options, CertificateRepository repository, CancellationToken ct) =>
{
    if (!Authorized(request, options)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(body.Reason)) return Results.BadRequest(new { error = "A revocation reason is required." });
    return await repository.RevokeAsync(publicId, body.Reason, ct) ? Results.NoContent() : Results.NotFound();
});
app.MapGet("/internal/audit/verify", async (HttpRequest request, IOptions<PlatformOptions> options, CertificateRepository repository, CancellationToken ct) => Authorized(request, options) ? Results.Ok(await repository.VerifyAuditChainAsync(ct)) : Results.Unauthorized());

await app.RunAsync();
