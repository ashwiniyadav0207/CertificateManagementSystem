using CertificateEngine.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CertificateEngine.Tests;

public sealed class OptionsValidationTests
{
    [Fact]
    public void PlatformOptionsAcceptACompleteLocalConfiguration()
    {
        var options = new PlatformOptions
        {
            PublicBaseUrl = "http://localhost:5001",
            DataDirectory = "data",
            InternalApiKey = new string('a', 32),
            PollIntervalSeconds = 90,
            MaxDeliveryAttempts = 5
        };

        Assert.False(new PlatformOptionsValidator().Validate(null, options).Failed);
    }

    [Theory]
    [InlineData("http://example.org")]
    [InlineData("not-a-url")]
    public void PlatformOptionsRejectNonHttpsNonLoopbackUrls(string publicBaseUrl)
    {
        var options = new PlatformOptions { PublicBaseUrl = publicBaseUrl, InternalApiKey = new string('a', 32) };

        var result = new PlatformOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("PublicBaseUrl", StringComparison.Ordinal));
    }

    [Fact]
    public void SigningOptionsRequireKeyMaterialWhenEnabled()
    {
        var result = new SigningOptionsValidator().Validate(null, new SigningOptions { Enabled = true });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("PfxPath", StringComparison.Ordinal));
    }
}
