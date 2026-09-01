using BlazorBase.User.Server.Controllers;
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
    SignInManager<TestUser> signInManager,
    RoleManager<IdentityRole> roleManager,
    AuthTokenIssuer<TestUser> tokenIssuer,
    TwoFactorPendingTokenService<TestUser> pendingTokenService)
    : AuthControllerBase<TestUser>(userManager, signInManager, roleManager, tokenIssuer, pendingTokenService);
