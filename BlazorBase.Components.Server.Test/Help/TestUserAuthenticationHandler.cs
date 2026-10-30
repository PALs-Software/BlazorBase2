using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Server.Test.Help;

/// <summary>
/// Authenticates a request as the user named in the <see cref="UserHeader"/> header, with the comma-separated roles
/// of <see cref="RolesHeader"/>, or leaves it unauthenticated when the header is absent.
/// </summary>
internal sealed class TestUserAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestUser";
    public const string UserHeader = "X-Test-User";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user) || String.IsNullOrEmpty(user))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new(ClaimTypes.Name, user.ToString()) };

        if (Request.Headers.TryGetValue(RolesHeader, out var roles))
            claims.AddRange(roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(role => new Claim(ClaimTypes.Role, role)));

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
