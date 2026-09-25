using BlazorBase.Speech.Server.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorBase.Speech.Server.Services;

/// <summary>Registers the BlazorBase.Speech server module.</summary>
public static class BlazorBaseSpeechServerServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="SpeechServerOptions"/>, registers the typed <see cref="ISpeechApiClient"/> and the
    /// per-user <see cref="SpeechRequestGate"/>.
    /// </summary>
    /// <remarks>
    /// The host derives a concrete controller from <c>SpeechControllerBase</c>. An empty
    /// <c>Speech:BaseUrl</c> is valid and switches speech off: the proxy then reports itself unavailable.
    /// <code>
    /// services.AddBlazorBaseSpeechServer(builder.Configuration);
    /// </code>
    /// </remarks>
    public static IServiceCollection AddBlazorBaseSpeechServer(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = SpeechServerOptions.SectionName)
    {
        services.AddLocalization();
        services.AddSingleton<IValidateOptions<SpeechServerOptions>, SpeechServerOptionsValidator>();
        services.AddOptions<SpeechServerOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateOnStart();

        services.AddSingleton<SpeechRequestGate>();
        services.AddHttpClient<ISpeechApiClient, SpeechApiClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SpeechServerOptions>>().Value;
            httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            if (options.IsConfigured)
                httpClient.BaseAddress = new Uri(options.BaseUrl!.TrimEnd('/') + "/");
        });

        return services;
    }
}
