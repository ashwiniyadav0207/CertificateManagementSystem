using Microsoft.Extensions.Options;

namespace CertificateEngine.Configuration;

public sealed class PlatformOptionsValidator : IValidateOptions<PlatformOptions>
{
    public ValidateOptionsResult Validate(string? name, PlatformOptions options)
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var publicUri)
            || (publicUri.Scheme != Uri.UriSchemeHttps
                && !(publicUri.Scheme == Uri.UriSchemeHttp && publicUri.IsLoopback)))
        {
            errors.Add("Platform:PublicBaseUrl must be an absolute HTTPS URL (or HTTP loopback for local testing).");
        }

        if (string.IsNullOrWhiteSpace(options.DataDirectory))
        {
            errors.Add("Platform:DataDirectory is required.");
        }

        if (options.InternalApiKey.Length < 32)
        {
            errors.Add("Platform:InternalApiKey must contain at least 32 characters.");
        }

        if (options.PollIntervalSeconds is < 15 or > 3600)
        {
            errors.Add("Platform:PollIntervalSeconds must be between 15 and 3600.");
        }

        if (options.MaxDeliveryAttempts is < 1 or > 20)
        {
            errors.Add("Platform:MaxDeliveryAttempts must be between 1 and 20.");
        }

        foreach (var eventOptions in options.Events)
        {
            if (string.IsNullOrWhiteSpace(eventOptions.EventId)
                || string.IsNullOrWhiteSpace(eventOptions.EventName)
                || string.IsNullOrWhiteSpace(eventOptions.SpreadsheetId))
            {
                errors.Add("Every Platform:Events entry requires EventId, EventName, and SpreadsheetId.");
            }

            if (eventOptions.MinimumScore.HasValue && string.IsNullOrWhiteSpace(eventOptions.ScoreColumn))
            {
                errors.Add($"Event '{eventOptions.EventId}' sets MinimumScore but not ScoreColumn.");
            }
        }

        var duplicateIds = options.Events
            .GroupBy(x => x.EventId, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key);
        foreach (var duplicateId in duplicateIds)
        {
            errors.Add($"Duplicate event ID '{duplicateId}'.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}

public sealed class SigningOptionsValidator : IValidateOptions<SigningOptions>
{
    public ValidateOptionsResult Validate(string? name, SigningOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.PfxPath))
        {
            return ValidateOptionsResult.Fail("Signing:PfxPath is required when signing is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.TrustedRootPath))
        {
            return ValidateOptionsResult.Fail("Signing:TrustedRootPath is required when signing is enabled.");
        }

        if (!string.IsNullOrWhiteSpace(options.TimestampServerUrl)
            && (!Uri.TryCreate(options.TimestampServerUrl, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Fail("Signing:TimestampServerUrl must be an absolute HTTPS URL.");
        }

        return ValidateOptionsResult.Success;
    }
}
