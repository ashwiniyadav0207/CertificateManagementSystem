namespace CertificateEngine.Domain;

public sealed class SourceDataException(string message) : Exception(message);

public sealed class DeliveryException(string message, bool isTransient, Exception? innerException = null)
    : Exception(message, innerException)
{
    public bool IsTransient { get; } = isTransient;
}
