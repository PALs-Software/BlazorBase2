using Microsoft.JSInterop;

namespace BlazorBase.Dashboard;

/// <summary>
/// Keeps dashboard layouts in the browser's <c>localStorage</c>, for hosts without server storage. Layouts live per browser
/// profile, not per user, and do not follow the user to another device.
/// </summary>
public sealed class LocalStorageDashboardLayoutStore(IJSRuntime jsRuntime) : IDashboardLayoutStore
{
    #region Injects

    private readonly IJSRuntime JsRuntime = jsRuntime;

    #endregion

    private const string KeyPrefix = "blazorbase.dashboardlayouts:";

    /// <inheritdoc />
    public async Task<DashboardLayout> GetAsync(string dashboardKey, CancellationToken cancellationToken = default)
    {
        var key = DashboardLayoutValidator.ValidateKey(dashboardKey);
        var json = await JsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, KeyPrefix + key);
        return DashboardLayoutSerializer.Deserialize(json);
    }

    /// <inheritdoc />
    public async Task SaveAsync(string dashboardKey, DashboardLayout layout, CancellationToken cancellationToken = default)
    {
        var key = DashboardLayoutValidator.ValidateKey(dashboardKey);
        var json = DashboardLayoutSerializer.Serialize(DashboardLayoutValidator.Normalize(layout));
        await JsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, KeyPrefix + key, json);
    }

    /// <inheritdoc />
    public async Task ResetAsync(string dashboardKey, CancellationToken cancellationToken = default)
    {
        var key = DashboardLayoutValidator.ValidateKey(dashboardKey);
        await JsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, KeyPrefix + key);
    }
}
