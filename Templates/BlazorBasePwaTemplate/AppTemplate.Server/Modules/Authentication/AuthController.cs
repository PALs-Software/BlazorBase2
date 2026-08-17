using AppTemplate.Server.Data;
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
    RoleManager<IdentityRole> roles,
    TokenService<AppUser> tokens,
    AppTemplateDbContext dbContext,
    IConfiguration configuration)
    : AuthControllerBase<AppUser>(users, roles, tokens, dbContext, configuration)
{
    protected override IReadOnlyList<string> RolesToSeed => RoleConstants.All;
}
