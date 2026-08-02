using CertificateEngine.Domain;

namespace CertificateEngine.Infrastructure.Delivery;

public sealed record DeliveryRequest(
    DeliveryRecord Delivery,
    CertificateRecord Certificate,
    Stream Artifact,
    string FileName,
    Uri VerificationUri);

public interface IDeliveryChannel
{
    DeliveryChannel Channel { get; }

    Task<SendResult> SendAsync(DeliveryRequest request, CancellationToken cancellationToken);
}
