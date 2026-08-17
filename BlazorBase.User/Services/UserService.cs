using System.Net.Http.Json;
using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

public class UserService(HttpClient httpClient) : IUserService
{
    #region Injects
    private readonly HttpClient HttpClient = httpClient;
    #endregion

    public async Task<UserProfile> GetMeAsync()
    {
        var response = await HttpClient.GetAsync("api/user/me");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfile>()
            ?? throw new InvalidOperationException("Invalid profile response.");
    }

    public async Task UpdateSettingsAsync(UpdateUserSettingsRequest request)
    {
        var response = await HttpClient.PutAsJsonAsync("api/user/me/settings", request);
        response.EnsureSuccessStatusCode();
    }
}
