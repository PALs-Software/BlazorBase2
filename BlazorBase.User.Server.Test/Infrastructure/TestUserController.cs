using BlazorBase.User.Models;
using BlazorBase.User.Server.Controllers;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// Concrete subclass of the abstract <see cref="UserControllerBase{TUser}"/> so the profile
/// endpoints can be instantiated and invoked directly in tests. Exposes
/// <see cref="SettingsUpdatedHook"/> so a test can observe the host-extensibility hook without a
/// dedicated subclass per test.
/// </summary>
public sealed class TestUserController(UserManager<TestUser> userManager, SignInManager<TestUser> signInManager)
    : UserControllerBase<TestUser>(userManager, signInManager)
{
    public Func<TestUser, UpdateUserSettingsRequest, Task>? SettingsUpdatedHook { get; set; }

    protected override Task OnSettingsUpdatedAsync(TestUser user, UpdateUserSettingsRequest request) =>
        SettingsUpdatedHook?.Invoke(user, request) ?? Task.CompletedTask;
}
