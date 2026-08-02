using CertificateEngine.Configuration;
using CertificateEngine.Domain;
using CertificateEngine.Infrastructure.Persistence;
using CertificateEngine.Infrastructure.Sheets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CertificateEngine.Certificates;

public sealed class CertificateIssuanceWorker(ISheetSource sheets, CertificateRepository repository, CertificateArtifactService artifacts, IOptions<PlatformOptions> platform, ILogger<CertificateIssuanceWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, int, Exception?> LogGenerationFailure = LoggerMessage.Define<string, int>(LogLevel.Error, new EventId(3001, nameof(LogGenerationFailure)), "Certificate generation failed for event {EventId}, row {RowNumber}");
    private static readonly Action<ILogger, string, Exception?> LogPollFailure = LoggerMessage.Define<string>(LogLevel.Error, new EventId(3002, nameof(LogPollFailure)), "Sheet poll failed for event {EventId}");
    public async Task PollEventAsync(EventSourceOptions eventOptions, CancellationToken cancellationToken)
    {
        var updates = new List<SheetCertificateUpdate>();
        foreach (var submission in await sheets.ReadApprovedSubmissionsAsync(eventOptions, cancellationToken))
        {
            var (certificate, _) = await repository.GetOrCreateAsync(submission, eventOptions, cancellationToken);
            try
            {
                if (certificate.Status == CertificateStatus.Pending)
                {
                    var artifact = await artifacts.CreateAsync(certificate, cancellationToken);
                    await repository.MarkIssuedAsync(certificate.Id, artifact.ArtifactPath, artifact.Sha256, artifact.SignerThumbprint, eventOptions, cancellationToken);
                    certificate = (await repository.FindByIdAsync(certificate.Id, cancellationToken))!;
                }
                updates.Add(new SheetCertificateUpdate(submission.RowNumber, certificate.Status == CertificateStatus.Issued ? "Generated" : certificate.Status.ToString(), certificate.Status == CertificateStatus.Issued ? certificate.PublicId : null, DateTimeOffset.UtcNow));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await repository.MarkIssuanceFailedAsync(certificate.Id, exception.Message, cancellationToken);
                updates.Add(new SheetCertificateUpdate(submission.RowNumber, "Failed", null, DateTimeOffset.UtcNow));
                LogGenerationFailure(logger, eventOptions.EventId, submission.RowNumber, exception);
            }
        }
        await sheets.WriteCertificateUpdatesAsync(eventOptions, updates, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var eventOptions in platform.Value.Events)
            {
                try { await PollEventAsync(eventOptions, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception exception) { LogPollFailure(logger, eventOptions.EventId, exception); }
            }
            await Task.Delay(TimeSpan.FromSeconds(platform.Value.PollIntervalSeconds), stoppingToken);
        }
    }
}
