using BlazorBase.User.Services;

namespace AppTemplate.Tests.Infrastructure;

public class StubUserLanguagePreferenceSync : IUserLanguagePreferenceSync
{
    public Task SaveAsync(string language) => Task.CompletedTask;
}
