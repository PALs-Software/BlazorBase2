using BlazorBase.User.Server.Controllers;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// Concrete subclass of the abstract <see cref="AuthControllerBase{TUser}"/> so the auth endpoints
/// can be instantiated and invoked directly in tests.
/// </summary>
public sealed class TestAuthController(
    UserManager<TestUser> userManager,
    RoleManager<IdentityRole> roleManager,
    TokenService<TestUser> tokenService,
    BaseUserDbContext<TestUser> dbContext,
    IConfiguration configuration)
    : AuthControllerBase<TestUser>(userManager, roleManager, tokenService, dbContext, configuration);
