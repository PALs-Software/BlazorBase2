using BlazorBase.Speech.Interop;
using BlazorBase.Speech.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBase.Speech.Services;

/// <summary>Registers the BlazorBase.Speech client module.</summary>
public static class BlazorBaseSpeechServiceCollectionExtensions
{
    /// <summary>
    /// Registers the push-to-talk recorder seam, audio playback, the narrator, text preparation and
    /// localized messages.
    /// </summary>
    /// <remarks>
    /// The host must additionally register the <see cref="HttpClient"/> behind
    /// <see cref="ISpeechClient"/>, because only the host knows the proxy's address and its
    /// authentication handler:
    /// <code>
    /// services.AddHttpClient&lt;ISpeechClient, HttpSpeechClient&gt;(client =&gt;
    ///         client.BaseAddress = new Uri(new Uri(hostBaseAddress), "api/speech/"))
    ///     .AddHttpMessageHandler&lt;AuthTokenHandler&gt;();
    /// </code>
    /// The server side is a controller derived from <c>SpeechControllerBase</c> in
    /// <c>BlazorBase.Speech.Server</c>.
    /// </remarks>
    public static IServiceCollection AddBlazorBaseSpeech(this IServiceCollection services)
    {
        services.AddLocalization();
        services.TryAddTransient<ISpeechRecorderInterop, SpeechRecorderInterop>();
        services.TryAddScoped<ISpeechAudioPlayer, JsSpeechAudioPlayer>();
        services.TryAddScoped<ISpeechNarrator, SpeechNarrator>();
        services.TryAddScoped<SpeechTextPreparer>();
        services.TryAddScoped<SpeechMessages>();

        return services;
    }
}
