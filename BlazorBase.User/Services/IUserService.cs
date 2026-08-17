using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

public interface IUserService
{
    Task<UserProfile> GetMeAsync();
    Task UpdateSettingsAsync(UpdateUserSettingsRequest request);
}
