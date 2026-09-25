using BlazorBase.Speech.Server.Configuration;
using BlazorBase.Speech.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Speech.Server.Test.Services;

public sealed class BlazorBaseSpeechServerServiceCollectionExtensionsTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddBlazorBaseSpeechServer(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void BindsTheSpeechSection()
    {
        using var provider = Build(new() { ["Speech:BaseUrl"] = "http://speech-api:8080", ["Speech:MaxTextCharacters"] = "1234" });

        var options = provider.GetRequiredService<IOptions<SpeechServerOptions>>().Value;

        Assert.Equal("http://speech-api:8080", options.BaseUrl);
        Assert.Equal(1234, options.MaxTextCharacters);
        Assert.NotNull(provider.GetRequiredService<ISpeechApiClient>());
        Assert.NotNull(provider.GetRequiredService<SpeechRequestGate>());
    }

    [Fact]
    public void MissingSection_MeansSpeechIsOff_NotAStartupFailure()
    {
        using var provider = Build([]);

        Assert.False(provider.GetRequiredService<IOptions<SpeechServerOptions>>().Value.IsConfigured);
        Assert.NotNull(provider.GetRequiredService<ISpeechApiClient>());
    }

    [Fact]
    public void MalformedBaseUrl_FailsValidation()
    {
        using var provider = Build(new() { ["Speech:BaseUrl"] = "speech-api:8080" });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SpeechServerOptions>>().Value);
    }
}
