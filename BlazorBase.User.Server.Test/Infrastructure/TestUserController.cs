using BlazorBase.User.Server.Controllers;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// Concrete subclass of the abstract <see cref="UserControllerBase{TUser}"/> so the profile
/// endpoints can be instantiated and invoked directly in tests.
/// </summary>
public sealed class TestUserController(UserManager<TestUser> userManager)
    : UserControllerBase<TestUser>(userManager);
