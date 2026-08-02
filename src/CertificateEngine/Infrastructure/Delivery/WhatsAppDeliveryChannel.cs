using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CertificateEngine.Configuration;
using CertificateEngine.Domain;
using Microsoft.Extensions.Options;

namespace CertificateEngine.Infrastructure.Delivery;

public sealed class WhatsAppDeliveryChannel : IDeliveryChannel
{
    public const string HttpClientName = "CertificateDelivery.WhatsApp";

    private const string GraphApiBaseUrl = "https://graph.facebook.com";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<WhatsAppOptions> _options;

    public WhatsAppDeliveryChannel(
        IHttpClientFactory httpClientFactory,
        IOptions<WhatsAppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public DeliveryChannel Channel => DeliveryChannel.WhatsApp;

    public async Task<SendResult> SendAsync(
        DeliveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.Value;
        var configurationError = ValidateConfiguration(options);
        if (configurationError is not null)
        {
            return SendResult.PermanentFailure(configurationError);
        }

        if (!TryNormalizePhoneNumber(request.Delivery.Destination, out var destination))
        {
            return SendResult.PermanentFailure(
                "The WhatsApp destination must be an international phone number containing 8 to 15 digits.");
        }

        if (string.IsNullOrWhiteSpace(request.Certificate.ParticipantName))
        {
            return SendResult.PermanentFailure(
                "The participant name required by the WhatsApp template is missing.");
        }

        if (!request.VerificationUri.IsAbsoluteUri
            || request.VerificationUri.Scheme != Uri.UriSchemeHttps)
        {
            return SendResult.PermanentFailure(
                "The WhatsApp template requires an absolute HTTPS verification URL.");
        }

        if (!request.Artifact.CanRead)
        {
            return SendResult.PermanentFailure("The certificate PDF cannot be read.");
        }

        if (!IsSafeFileName(request.FileName))
        {
            return SendResult.PermanentFailure(
                "The certificate PDF filename is missing or invalid.");
        }

        try
        {
            if (request.Artifact.CanSeek)
            {
                request.Artifact.Position = 0;
                if (request.Artifact.Length == 0)
                {
                    return SendResult.PermanentFailure("The certificate PDF is empty.");
                }
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var (mediaId, uploadFailure) = await UploadPdfAsync(
                client,
                options,
                request,
                cancellationToken);
            if (uploadFailure is not null)
            {
                return uploadFailure;
            }

            if (string.IsNullOrWhiteSpace(mediaId))
            {
                return SendResult.TransientFailure(
                    "WhatsApp accepted the PDF upload but did not return a media identifier.");
            }

            return await SendTemplateAsync(
                client,
                options,
                destination,
                mediaId,
                request,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return SendResult.TransientFailure(
                "The WhatsApp request timed out; the delivery can be retried.");
        }
        catch (HttpRequestException)
        {
            return SendResult.TransientFailure(
                "The WhatsApp API could not be reached; the delivery can be retried.");
        }
        catch (IOException)
        {
            return SendResult.TransientFailure(
                "An I/O error occurred while uploading the certificate; the delivery can be retried.");
        }
        catch (ArgumentException)
        {
            return SendResult.PermanentFailure(
                "The WhatsApp API request configuration is invalid.");
        }
        catch (FormatException)
        {
            return SendResult.PermanentFailure(
                "The WhatsApp API request configuration has an invalid format.");
        }
        catch (InvalidOperationException)
        {
            return SendResult.PermanentFailure(
                "The WhatsApp API request could not be created from the configured values.");
        }
    }

    private static async Task<(string? MediaId, SendResult? Failure)> UploadPdfAsync(
        HttpClient client,
        WhatsAppOptions options,
        DeliveryRequest request,
        CancellationToken cancellationToken)
    {
        using var uploadRequest = CreateRequest(
            HttpMethod.Post,
            CreateGraphUri(options, "media"),
            options.AccessToken);
        using var multipart = new MultipartFormDataContent();
        using var fileContent = new StreamContent(request.Artifact);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipart.Add(new StringContent("whatsapp", Encoding.UTF8), "messaging_product");
        multipart.Add(new StringContent("application/pdf", Encoding.UTF8), "type");
        multipart.Add(fileContent, "file", request.FileName);
        uploadRequest.Content = multipart;

        using var response = await client.SendAsync(
            uploadRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, CreateHttpFailure(response.StatusCode, "PDF upload"));
        }

        var mediaId = await ReadRootStringAsync(response, "id", cancellationToken);
        return string.IsNullOrWhiteSpace(mediaId)
            ? (null, SendResult.TransientFailure(
                "WhatsApp returned an invalid PDF upload response; the delivery can be retried."))
            : (mediaId, null);
    }

    private static async Task<SendResult> SendTemplateAsync(
        HttpClient client,
        WhatsAppOptions options,
        string destination,
        string mediaId,
        DeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = destination,
            type = "template",
            template = new
            {
                name = options.TemplateName,
                language = new { code = options.TemplateLanguage },
                components = new object[]
                {
                    new
                    {
                        type = "header",
                        parameters = new object[]
                        {
                            new
                            {
                                type = "document",
                                document = new
                                {
                                    id = mediaId,
                                    filename = request.FileName
                                }
                            }
                        }
                    },
                    new
                    {
                        type = "body",
                        parameters = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = request.Certificate.ParticipantName
                            },
                            new
                            {
                                type = "text",
                                text = request.VerificationUri.AbsoluteUri
                            }
                        }
                    }
                }
            }
        };

        using var content = JsonContent.Create(payload);
        using var messageRequest = CreateRequest(
            HttpMethod.Post,
            CreateGraphUri(options, "messages"),
            options.AccessToken);
        messageRequest.Content = content;

        using var response = await client.SendAsync(
            messageRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return CreateHttpFailure(response.StatusCode, "template send");
        }

        var providerMessageId = await ReadMessageIdAsync(response, cancellationToken);
        return string.IsNullOrWhiteSpace(providerMessageId)
            ? SendResult.TransientFailure(
                "WhatsApp accepted the template request but did not return a message identifier.")
            : SendResult.Sent(providerMessageId);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        Uri uri,
        string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static Uri CreateGraphUri(WhatsAppOptions options, string resource) =>
        new($"{GraphApiBaseUrl}/{options.GraphApiVersion}/{options.PhoneNumberId}/{resource}");

    private static SendResult CreateHttpFailure(HttpStatusCode statusCode, string operation)
    {
        var numericStatus = (int)statusCode;
        if (numericStatus is 408 or 409 or 425 or 429 || numericStatus >= 500)
        {
            return SendResult.TransientFailure(
                $"WhatsApp {operation} failed with HTTP {numericStatus}; the delivery can be retried.");
        }

        var guidance = numericStatus switch
        {
            400 => "verify the destination and approved template parameters",
            401 or 403 => "verify the access token and WhatsApp permissions",
            404 => "verify the Graph API version and phone number ID",
            413 => "reduce the certificate PDF size",
            _ => "verify the WhatsApp request and channel configuration"
        };
        return SendResult.PermanentFailure(
            $"WhatsApp {operation} failed with HTTP {numericStatus}; {guidance}.");
    }

    private static async Task<string?> ReadRootStringAsync(
        HttpResponseMessage response,
        string propertyName,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = property.GetString();
            return value is { Length: > 0 and <= 512 } ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task<string?> ReadMessageIdAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("messages", out var messages)
                || messages.ValueKind != JsonValueKind.Array
                || messages.GetArrayLength() == 0)
            {
                return null;
            }

            var firstMessage = messages[0];
            if (!firstMessage.TryGetProperty("id", out var id)
                || id.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = id.GetString();
            return value is { Length: > 0 and <= 512 } ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? ValidateConfiguration(WhatsAppOptions options)
    {
        if (!options.Enabled)
        {
            return "WhatsApp delivery is disabled; enable the WhatsApp channel before retrying.";
        }

        if (!IsGraphApiVersion(options.GraphApiVersion))
        {
            return "WhatsApp GraphApiVersion must use a value such as 'v23.0'.";
        }

        if (string.IsNullOrWhiteSpace(options.PhoneNumberId)
            || options.PhoneNumberId.Any(character => !char.IsAsciiDigit(character)))
        {
            return "WhatsApp PhoneNumberId is required and must contain only digits.";
        }

        if (string.IsNullOrWhiteSpace(options.AccessToken)
            || options.AccessToken.Any(char.IsWhiteSpace))
        {
            return "WhatsApp AccessToken is required and must not contain whitespace.";
        }

        if (string.IsNullOrWhiteSpace(options.TemplateName))
        {
            return "WhatsApp TemplateName is required when the channel is enabled.";
        }

        if (string.IsNullOrWhiteSpace(options.TemplateLanguage))
        {
            return "WhatsApp TemplateLanguage is required when the channel is enabled.";
        }

        return null;
    }

    private static bool IsGraphApiVersion(string value)
    {
        if (value.Length < 4 || value[0] != 'v')
        {
            return false;
        }

        var dotIndex = value.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex <= 1 || dotIndex == value.Length - 1)
        {
            return false;
        }

        return value.AsSpan(1, dotIndex - 1).IndexOfAnyExceptInRange('0', '9') < 0
            && value.AsSpan(dotIndex + 1).IndexOfAnyExceptInRange('0', '9') < 0;
    }

    private static bool TryNormalizePhoneNumber(string value, out string normalized)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsAsciiDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (character is not ('+' or ' ' or '-' or '(' or ')' or '.'))
            {
                normalized = string.Empty;
                return false;
            }
        }

        normalized = builder.ToString();
        return normalized.Length is >= 8 and <= 15;
    }

    private static bool IsSafeFileName(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.IndexOfAny(['\r', '\n']) < 0
        && string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal);
}
