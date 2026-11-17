using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Services;

/// <summary>Registers the client of the invitation API.</summary>
public static class BlazorBaseUserInvitationClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IUserInvitationService"/> as a typed <see cref="HttpClient"/> configured by
    /// <paramref name="configureClient"/>, and returns its builder so the host adds the handlers of its
    /// transport: <c>.AddBlazorBaseCookieSessionHandlers()</c> under the cookie transport (the anonymous accept
    /// call needs its antiforgery header too), <c>.AddHttpMessageHandler&lt;AuthTokenHandler&gt;()</c> under
    /// the bearer transport.
    /// </summary>
    public static IHttpClientBuilder AddBlazorBaseUserInvitationClient(this IServiceCollection services, Action<HttpClient> configureClient)
    {
        ArgumentNullException.ThrowIfNull(configureClient);

        services.AddOptions<BlazorBaseUserClientOptions>();
        return services.AddHttpClient<IUserInvitationService, UserInvitationService>(configureClient);
    }
}
