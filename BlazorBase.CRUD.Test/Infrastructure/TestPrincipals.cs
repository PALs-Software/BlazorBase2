using System.Security.Claims;

namespace BlazorBase.CRUD.Test.Infrastructure;

/// <summary>
/// Builds <see cref="ClaimsPrincipal"/> instances for the access-rights tests.
/// </summary>
public static class TestPrincipals
{
    public static ClaimsPrincipal Anonymous()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    public static ClaimsPrincipal WithRoles(params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth"));
    }
}
