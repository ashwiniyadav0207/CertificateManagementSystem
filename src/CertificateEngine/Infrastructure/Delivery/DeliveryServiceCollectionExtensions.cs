using CertificateEngine.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CertificateEngine.Infrastructure.Delivery;

public static class DeliveryServiceCollectionExtensions
{
    public static IServiceCollection AddCertificateDelivery(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions();
        services.AddHttpClient(WhatsAppDeliveryChannel.HttpClientName);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDeliveryChannel, SmtpDeliveryChannel>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDeliveryChannel, WhatsAppDeliveryChannel>());
        services.AddHostedService<DeliveryWorker>();
        return services;
    }

    public static IServiceCollection AddCertificateDelivery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SmtpOptions>(
            configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<WhatsAppOptions>(
            configuration.GetSection(WhatsAppOptions.SectionName));
        return services.AddCertificateDelivery();
    }
}
