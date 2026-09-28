using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.CRUD.Test.Endpoints;

/// <summary>
/// Authenticates every request as a fixed, always-authenticated test principal with no roles —
/// sufficient for <see cref="BaseEndpointMapperConfiguratorTests"/>, whose test entity carries no
/// <see cref="Attributes.CrudAccessAttribute"/> and therefore only requires an authenticated user
/// to pass the endpoints' class-level gate.
/// </summary>
internal sealed class AlwaysAuthenticatedHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AlwaysAuthenticated";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-caller")], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
