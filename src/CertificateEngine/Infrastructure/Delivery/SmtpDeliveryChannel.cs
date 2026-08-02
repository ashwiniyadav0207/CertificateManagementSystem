using System.Net;
using System.Net.Sockets;
using CertificateEngine.Configuration;
using CertificateEngine.Domain;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;

namespace CertificateEngine.Infrastructure.Delivery;

public sealed class SmtpDeliveryChannel : IDeliveryChannel
{
    private readonly IOptions<SmtpOptions> _options;

    public SmtpDeliveryChannel(IOptions<SmtpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public DeliveryChannel Channel => DeliveryChannel.Email;

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

        if (!MailboxAddress.TryParse(options.FromAddress, out var parsedFrom) || parsedFrom is null)
        {
            return SendResult.PermanentFailure(
                "SMTP FromAddress is not a valid single email address.");
        }

        if (!MailboxAddress.TryParse(request.Delivery.Destination, out var recipient) || recipient is null)
        {
            return SendResult.PermanentFailure(
                "The email delivery destination is not a valid single email address.");
        }

        if (!request.Artifact.CanRead)
        {
            return SendResult.PermanentFailure("The certificate PDF cannot be read.");
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return SendResult.PermanentFailure("The certificate attachment filename is missing.");
        }

        var from = string.IsNullOrWhiteSpace(options.FromName)
            ? parsedFrom
            : new MailboxAddress(SanitizeHeader(options.FromName), parsedFrom.Address);

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

            var messageId = MimeUtils.GenerateMessageId();
            using var message = CreateMessage(
                request,
                from,
                recipient,
                messageId,
                cancellationToken);
            using var client = new SmtpClient();

            try
            {
                var socketOptions = options.UseStartTls
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.Auto;
                await client.ConnectAsync(
                    options.Host,
                    options.Port,
                    socketOptions,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(options.Username))
                {
                    await client.AuthenticateAsync(
                        options.Username,
                        options.Password,
                        cancellationToken);
                }

                await client.SendAsync(message, cancellationToken);
                return SendResult.Sent(messageId);
            }
            finally
            {
                await DisconnectSafelyAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MailKit.Security.AuthenticationException)
        {
            return SendResult.PermanentFailure(
                "SMTP authentication failed; verify the configured username and password.");
        }
        catch (SslHandshakeException)
        {
            return SendResult.PermanentFailure(
                "SMTP TLS negotiation failed; verify the host, port, STARTTLS setting, and server certificate.");
        }
        catch (SmtpCommandException exception)
        {
            var statusCode = (int)exception.StatusCode;
            return statusCode is >= 400 and < 500
                ? SendResult.TransientFailure(
                    $"The SMTP server temporarily rejected the message (status {statusCode}).")
                : SendResult.PermanentFailure(
                    $"The SMTP server rejected the message (status {statusCode}); verify the sender and recipient settings.");
        }
        catch (NotSupportedException)
        {
            return SendResult.PermanentFailure(
                "The SMTP server does not support the configured STARTTLS or authentication mode.");
        }
        catch (ArgumentException)
        {
            return SendResult.PermanentFailure(
                "The SMTP message or connection settings are invalid.");
        }
        catch (ServiceNotConnectedException)
        {
            return SendResult.TransientFailure(
                "The SMTP connection closed before delivery completed; the delivery can be retried.");
        }
        catch (InvalidOperationException)
        {
            return SendResult.PermanentFailure(
                "The SMTP client could not use the configured connection or authentication settings.");
        }
        catch (SmtpProtocolException)
        {
            return SendResult.TransientFailure(
                "The SMTP server returned an invalid or incomplete response; the delivery can be retried.");
        }
        catch (SocketException)
        {
            return SendResult.TransientFailure(
                "The SMTP server could not be reached; the delivery can be retried.");
        }
        catch (IOException)
        {
            return SendResult.TransientFailure(
                "An I/O error occurred while sending email; the delivery can be retried.");
        }
        catch (TimeoutException)
        {
            return SendResult.TransientFailure(
                "The SMTP operation timed out; the delivery can be retried.");
        }
    }

    private static string? ValidateConfiguration(SmtpOptions options)
    {
        if (!options.Enabled)
        {
            return "Email delivery is disabled; enable the Smtp channel before retrying.";
        }

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            return "SMTP Host is required when email delivery is enabled.";
        }

        if (options.Port is < 1 or > 65535)
        {
            return "SMTP Port must be between 1 and 65535.";
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            return "SMTP FromAddress is required when email delivery is enabled.";
        }

        var hasUsername = !string.IsNullOrWhiteSpace(options.Username);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);
        if (hasUsername != hasPassword)
        {
            return "SMTP Username and Password must either both be configured or both be empty.";
        }

        return null;
    }

    private static MimeMessage CreateMessage(
        DeliveryRequest request,
        MailboxAddress from,
        MailboxAddress recipient,
        string messageId,
        CancellationToken cancellationToken)
    {
        var certificate = request.Certificate;
        var participantName = WebUtility.HtmlEncode(certificate.ParticipantName);
        var eventName = WebUtility.HtmlEncode(certificate.EventName);
        var verificationUrl = WebUtility.HtmlEncode(request.VerificationUri.AbsoluteUri);

        var body = new BodyBuilder
        {
            TextBody = $"""
                Hello {certificate.ParticipantName},

                Your certificate for {certificate.EventName} is attached.

                Verify it at: {request.VerificationUri.AbsoluteUri}
                """,
            HtmlBody = $"""
                <p>Hello {participantName},</p>
                <p>Your certificate for {eventName} is attached.</p>
                <p>Verify it at <a href="{verificationUrl}">{verificationUrl}</a>.</p>
                """
        };
        body.Attachments.Add(
            request.FileName,
            request.Artifact,
            new ContentType("application", "pdf"),
            cancellationToken);

        var message = new MimeMessage
        {
            MessageId = messageId,
            Subject = SanitizeHeader($"Your {certificate.EventName} certificate"),
            Body = body.ToMessageBody()
        };
        message.From.Add(from);
        message.To.Add(recipient);
        return message;
    }

    private static async Task DisconnectSafelyAsync(
        SmtpClient client,
        CancellationToken cancellationToken)
    {
        if (!client.IsConnected)
        {
            return;
        }

        try
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Disposing the client below still closes the connection.
        }
        catch (ServiceNotConnectedException)
        {
            // The server closed the connection before the QUIT command.
        }
        catch (SmtpCommandException)
        {
            // Delivery outcome must not be changed by a failed QUIT command.
        }
        catch (SmtpProtocolException)
        {
            // Delivery outcome must not be changed by a failed QUIT command.
        }
        catch (SocketException)
        {
            // Delivery outcome must not be changed by a failed QUIT command.
        }
        catch (IOException)
        {
            // Delivery outcome must not be changed by a failed QUIT command.
        }
    }

    private static string SanitizeHeader(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
