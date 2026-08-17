using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

public interface ITokenStorage
{
    Task<LoginResponse?> GetTokensAsync();
    Task SaveTokensAsync(LoginResponse tokens);
    Task ClearTokensAsync();
}
