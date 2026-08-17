using Microsoft.AspNetCore.Components;
using BlazorBase.User.Services;

namespace BlazorBase.User.Pages;

public partial class SetupServer(IAppConfigService appConfig, NavigationManager navigation)
{
    #region Injects
    private readonly IAppConfigService AppConfig = appConfig;
    private readonly NavigationManager Navigation = navigation;
    #endregion

    private string ServerUrl { get; set; } = string.Empty;
    private string? ErrorMessage { get; set; }
    private bool IsTesting { get; set; }
    private bool IsSuccess { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (OperatingSystem.IsBrowser())
        {
            Navigation.NavigateTo("/login", forceLoad: false);
            return;
        }

        var existingUrl = await AppConfig.GetServerUrlAsync();
        if (!string.IsNullOrEmpty(existingUrl))
            ServerUrl = existingUrl;
    }

    private async Task HandleSave()
    {
        ErrorMessage = null;
        IsSuccess = false;

        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            ErrorMessage = "Please enter a server URL.";
            return;
        }

        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            ErrorMessage = "Please enter a valid URL (https://...).";
            return;
        }

        IsTesting = true;
        try
        {
            using var http = new HttpClient { BaseAddress = uri };
            var response = await http.GetAsync("api/auth/status");
            response.EnsureSuccessStatusCode();

            await AppConfig.SaveServerUrlAsync(ServerUrl);
            IsSuccess = true;

            await Task.Delay(500);
            Navigation.NavigateTo("/login", forceLoad: true);
        }
        catch
        {
            ErrorMessage = "Could not connect to the server. Please check the URL.";
        }
        finally
        {
            IsTesting = false;
        }
    }
}
