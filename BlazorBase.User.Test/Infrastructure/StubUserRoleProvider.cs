using BlazorBase.User.Services;

namespace BlazorBase.User.Test.Infrastructure;

/// <summary>
/// Test role provider returning a fixed set of role names so the role input can be exercised without
/// an app-specific implementation.
/// </summary>
public sealed class StubUserRoleProvider(params string[] roleNames) : IUserRoleProvider
{
    private readonly IReadOnlyList<string> Roles = roleNames;

    public IReadOnlyList<string> GetRoleNames() => Roles;
}
