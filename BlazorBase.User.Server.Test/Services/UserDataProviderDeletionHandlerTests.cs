using BlazorBase.User.Server.Lifecycle;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Server.Test.Services;

/// <summary>
/// Covers the user-deletion seam on <see cref="UserDataProvider{TUser}"/>: every registered
/// <see cref="IUserDeletionHandler"/> runs exactly once, with the deleted user's id, after a
/// successful delete, and none of them run when the self-delete guard blocks the delete.
/// </summary>
public sealed class UserDataProviderDeletionHandlerTests : IdentityServerTestBase
{
    [Fact]
    public async Task Delete_InvokesEveryRegisteredHandler_ExactlyOnceWithTheDeletedUserId()
    {
        var user = await SeedUserAsync("deleted@example.com", "Deleted User");
        var firstHandler = new MockUserDeletionHandler();
        var secondHandler = new MockUserDeletionHandler();

        using var scope = CreateScope();
        var provider = CreateProvider(scope, firstHandler, secondHandler);

        await provider.DeleteAsync(user.Id);

        Assert.Equal([user.Id], firstHandler.InvokedUserIds);
        Assert.Equal([user.Id], secondHandler.InvokedUserIds);
    }

    [Fact]
    public async Task Delete_DoesNotInvokeHandlers_WhenSelfDeleteGuardBlocksTheDelete()
    {
        var user = await SeedUserAsync("self@example.com", "Self");
        SetCurrentUser(user.Id);
        var handler = new MockUserDeletionHandler();

        using var scope = CreateScope();
        var provider = CreateProvider(scope, handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.DeleteAsync(user.Id));

        Assert.Empty(handler.InvokedUserIds);
    }

    private static UserDataProvider<TestUser> CreateProvider(IServiceScope scope, params IUserDeletionHandler[] deletionHandlers)
        => new(
            scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<TestUserDbContext>(),
            deletionHandlers);

    private sealed class MockUserDeletionHandler : IUserDeletionHandler
    {
        public List<string> InvokedUserIds { get; } = [];

        public Task OnUserDeletedAsync(string userId, CancellationToken cancellationToken = default)
        {
            InvokedUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }
}
