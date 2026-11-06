using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorBase.User.Services;

public static class BlazorBaseDevelopmentSessionExtensions
{
    /// <summary>
    /// Asks the server whether it offers development authentication and, if so, signs in as its
    /// configured account before the app renders — so a developer lands on the dashboard instead
    /// of the login form. Does nothing at all against a server that does not offer it, which is
    /// every environment but Development, so this is safe to call unconditionally.
    /// </summary>
    /// <remarks>
    /// Call after <c>builder.Build()</c> and before <c>RunAsync()</c>:
    /// <code>
    /// var host = builder.Build();
    /// await host.Services.UseBlazorBaseDevelopmentSessionAsync();
    /// await host.RunAsync();
    /// </code>
    /// It signs in on every start rather than reusing a stored token, because a token minted
    /// before the roles were changed in configuration would keep the old ones and quietly defeat
    /// the point of switching them.
    ///
    /// Works under both transports. Under the cookie transport the login is a POST like any other and
    /// needs the antiforgery token, so an anonymous session is read first; the sign-in then goes
    /// through <see cref="BlazorBaseUserAuthStateProvider.MarkUserAsAuthenticated"/>, exactly like the
    /// login page, which also replaces that anonymous token with one bound to the new identity.
    /// </remarks>
    public static async Task UseBlazorBaseDevelopmentSessionAsync(this IServiceProvider serviceProvider)
    {
        var authService = serviceProvider.GetService<IAuthService>();
        var authStateProvider = serviceProvider.GetService<BlazorBaseUserAuthStateProvider>();

        if (authService is null || authStateProvider is null)
            return;

        var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("BlazorBase.DevelopmentSession");

        try
        {
            var status = await authService.GetStatusAsync();

            if (!status.DevelopmentAuthenticationEnabled)
                return;

            await EnsureAntiforgeryTokenAsync(serviceProvider, authService);

            var tokens = await authService.DevelopmentLoginAsync();

            if (tokens is null)
            {
                logger?.LogWarning("The server announced development authentication but refused to issue a session.");
                return;
            }

            await authStateProvider.MarkUserAsAuthenticated(tokens);
            logger?.LogInformation("Signed in through development authentication; no login required.");
        }
        catch (Exception exception)
        {
            // A developer convenience must never keep the app from starting — fall through to the
            // normal login form instead.
            logger?.LogWarning(exception, "Development authentication could not be established; falling back to the login form.");
        }
    }

    /// <summary>
    /// Nothing has asked for the antiforgery token this early in the start-up. The anonymous session
    /// answer carries one under the cookie transport; under the bearer transport it carries none and
    /// this changes nothing.
    /// </summary>
    private static async Task EnsureAntiforgeryTokenAsync(IServiceProvider serviceProvider, IAuthService authService)
    {
        var csrfTokenProvider = serviceProvider.GetService<CsrfTokenProvider>();

        if (csrfTokenProvider is null || csrfTokenProvider.Token is not null)
            return;

        var anonymousSession = await authService.GetSessionAsync();
        csrfTokenProvider.Set(anonymousSession.CsrfToken);
    }
}
