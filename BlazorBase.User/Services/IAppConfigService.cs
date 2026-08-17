namespace BlazorBase.User.Services;

public interface IAppConfigService
{
    Task<string?> GetServerUrlAsync();
    Task SaveServerUrlAsync(string url);
    Task<bool> IsConfiguredAsync();
}
