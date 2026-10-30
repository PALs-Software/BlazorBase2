using System.Net;
using System.Net.Http.Json;

namespace BlazorBase.Dashboard;

/// <summary>
/// Stores dashboard layouts on the server through the endpoints of <c>MapBlazorBaseDashboardLayouts</c>. Used by WASM and MAUI
/// clients; the server derives the user from the authenticated request, so the client never names one.
/// </summary>
/// <remarks>
/// The <see cref="HttpClient"/> base address must point at the dashboard-layout route and end with a slash.
/// </remarks>
public sealed class HttpDashboardLayoutStore(HttpClient httpClient) : IDashboardLayoutStore
{
    #region Injects

    private readonly HttpClient HttpClient = httpClient;

    #endregion

    /// <inheritdoc />
    public async Task<DashboardLayout> GetAsync(string dashboardKey, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync(BuildUrl(dashboardKey), cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DashboardLayout>(cancellationToken) ?? new DashboardLayout();
    }

    /// <inheritdoc />
    public async Task SaveAsync(string dashboardKey, DashboardLayout layout, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync(BuildUrl(dashboardKey), layout, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new DashboardLayoutValidationException(await ReadMessageAsync(response, cancellationToken));

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task ResetAsync(string dashboardKey, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync(BuildUrl(dashboardKey), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return;

        response.EnsureSuccessStatusCode();
    }

    private static string BuildUrl(string dashboardKey) =>
        $"?dashboardKey={Uri.EscapeDataString(dashboardKey)}";

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return String.IsNullOrWhiteSpace(text) ? "The layout was rejected." : text.Trim('"');
    }
}
