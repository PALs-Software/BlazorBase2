using BlazorBase.User.Server.Controllers;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// Concrete subclass of the abstract <see cref="AuthControllerBase{TUser}"/> so the auth endpoints
/// can be instantiated and invoked directly in tests. Exposes <see cref="DevelopmentLoginHook"/> so
/// a test can observe the post-development-login extensibility hook without a dedicated subclass
/// per test.
/// </summary>
public sealed class TestAuthController(
    UserManager<TestUser> userManager,
    SignInManager<TestUser> signInManager,
    RoleManager<IdentityRole> roleManager,
    AuthTokenIssuer<TestUser> tokenIssuer,
    TwoFactorPendingTokenService<TestUser> pendingTokenService)
    : AuthControllerBase<TestUser>(userManager, signInManager, roleManager, tokenIssuer, pendingTokenService)
{
    public Func<TestUser, Task>? DevelopmentLoginHook { get; set; }

    protected override Task OnAfterDevelopmentLoginAsync(TestUser user) =>
        DevelopmentLoginHook?.Invoke(user) ?? Task.CompletedTask;
}
