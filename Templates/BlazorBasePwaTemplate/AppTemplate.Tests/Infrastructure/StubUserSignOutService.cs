using BlazorBase.User.Services;

namespace AppTemplate.Tests.Infrastructure;

public class StubUserSignOutService : IUserSignOutService
{
    public int SignOutCount { get; private set; }

    public Task<SignOutResult> SignOutAsync()
    {
        SignOutCount++;

        return Task.FromResult(SignOutResult.SignedOut);
    }
}
