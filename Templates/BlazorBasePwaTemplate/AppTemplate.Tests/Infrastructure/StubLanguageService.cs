using BlazorBase.Components.Services;

namespace AppTemplate.Tests.Infrastructure;

public class StubLanguageService : ILanguageService
{
    public string CurrentLanguage { get; private set; } = "en";

    public event Action? LanguageChanged;

    public Task SetLanguageAsync(string cultureCode)
    {
        CurrentLanguage = cultureCode;
        LanguageChanged?.Invoke();

        return Task.CompletedTask;
    }

    public void ApplyStartupLanguage(string cultureCode) => CurrentLanguage = cultureCode;

    public Task InitializeAsync() => Task.CompletedTask;
}
