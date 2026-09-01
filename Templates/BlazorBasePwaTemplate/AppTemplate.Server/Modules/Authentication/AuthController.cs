using AppTemplate.Server.Entities;
using AppTemplate.Shared.Modules.Authentication;
using BlazorBase.User.Server.Controllers;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AppTemplate.Server.Modules.Authentication;

/// <summary>
/// Concrete auth endpoint so ASP.NET Core discovers the abstract base implementation.
/// </summary>
[ApiController]
public class AuthController(
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    RoleManager<IdentityRole> roles,
    AuthTokenIssuer<AppUser> tokenIssuer,
    IConfiguration configuration)
    : AuthControllerBase<AppUser>(users, signIn, roles, tokenIssuer, configuration)
{
    protected override IReadOnlyList<string> RolesToSeed => RoleConstants.All;
}
