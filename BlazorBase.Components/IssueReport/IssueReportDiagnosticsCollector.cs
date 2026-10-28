using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using BlazorBase.Components.Diagnostics;
using BlazorBase.Components.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.IssueReport;

/// <summary>Gathers the technical context for an issue report.</summary>
public interface IIssueReportDiagnosticsCollector
{
    /// <summary>Reads the current page, user, browser and client diagnostics.</summary>
    Task<IssueReportDiagnostics> CollectAsync();
}

/// <summary>
/// Default <see cref="IIssueReportDiagnosticsCollector"/>. It reads the navigation state, the optional
/// <see cref="IClientDiagnostics"/>, <see cref="IThemeService"/> and <see cref="AuthenticationStateProvider"/> and the
/// browser's user agent; anything that is not registered is left out.
/// </summary>
public sealed class IssueReportDiagnosticsCollector(
    NavigationManager navigationManager,
    IIssueReportBrowser browser,
    IOptions<IssueReportOptions> options,
    IServiceProvider serviceProvider) : IIssueReportDiagnosticsCollector
{
    #region Injects

    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IIssueReportBrowser Browser = browser;
    private readonly IssueReportOptions Options = options.Value;
    private readonly IServiceProvider ServiceProvider = serviceProvider;

    #endregion

    /// <inheritdoc />
    public async Task<IssueReportDiagnostics> CollectAsync()
    {
        var clientDiagnostics = ServiceProvider.GetService<IClientDiagnostics>();

        return new IssueReportDiagnostics
        {
            PageUrl = NavigationManager.Uri,
            Route = "/" + NavigationManager.ToBaseRelativePath(NavigationManager.Uri),
            TimeUtc = DateTimeOffset.UtcNow,
            App = FormatApp(),
            User = await ReadUserAsync(),
            CorrelationId = clientDiagnostics?.LastCorrelationId,
            Culture = CultureInfo.CurrentUICulture.Name,
            Theme = ServiceProvider.GetService<IThemeService>()?.CurrentTheme.ToString(),
            Browser = await Browser.GetUserAgentAsync(),
            Errors = clientDiagnostics?.Errors.Select(entry => $"{entry.TimeUtc:HH:mm:ss}Z {entry.Source}: {entry.Message}").ToList() ?? [],
        };
    }

    private string? FormatApp()
    {
        var version = Options.AppVersion ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
        var parts = new[] { Options.SiteName, version }.Where(part => !string.IsNullOrWhiteSpace(part));

        return string.Join(' ', parts);
    }

    private async Task<string?> ReadUserAsync()
    {
        var provider = ServiceProvider.GetService<AuthenticationStateProvider>();
        if (provider is null)
            return null;

        var user = (await provider.GetAuthenticationStateAsync()).User;
        if (user.Identity?.IsAuthenticated != true)
            return null;

        return user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value
            ?? user.FindFirst("preferred_username")?.Value
            ?? user.Identity.Name;
    }
}
