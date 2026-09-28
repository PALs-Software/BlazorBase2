using BlazorBase.Speech.Interop;
using BlazorBase.Speech.Services;
using BlazorBase.Speech.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace BlazorBase.Speech.Test.Services;

public sealed class BlazorBaseSpeechServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddSingleton(Substitute.For<ISpeechClient>());
        services.AddBlazorBaseSpeech();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void RegistersEveryService()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISpeechAudioPlayer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISpeechNarrator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISpeechRecorderInterop>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SpeechTextPreparer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SpeechMessages>());
    }

    [Fact]
    public void ASynchronouslyDisposedScope_DoesNotThrow()
    {
        using var provider = BuildProvider();
        var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ISpeechAudioPlayer>();
        scope.ServiceProvider.GetRequiredService<ISpeechRecorderInterop>();

        var exception = Record.Exception(scope.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public async Task AnAsynchronouslyDisposedScope_DoesNotThrow()
    {
        await using var provider = BuildProvider();
        var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ISpeechAudioPlayer>();
        scope.ServiceProvider.GetRequiredService<ISpeechRecorderInterop>();

        var exception = await Record.ExceptionAsync(async () => await scope.DisposeAsync());

        Assert.Null(exception);
    }
}
