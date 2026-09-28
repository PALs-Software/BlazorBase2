using BlazorBase.User.Server.Controllers;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>
/// Concrete auth endpoint so ASP.NET Core's controller discovery finds the abstract base
/// implementation. Public because <c>MapControllers()</c> only discovers public controller types.
/// </summary>
[ApiController]
public sealed class HarnessAuthController(
    UserManager<HarnessUser> users,
    SignInManager<HarnessUser> signIn,
    RoleManager<IdentityRole> roles,
    AuthTokenIssuer<HarnessUser> tokenIssuer,
    TwoFactorPendingTokenService<HarnessUser> pendingTokens)
    : AuthControllerBase<HarnessUser>(users, signIn, roles, tokenIssuer, pendingTokens);
