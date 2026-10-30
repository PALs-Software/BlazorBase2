using BlazorBase.Components.Diagnostics;
using BlazorBase.Components.HeaderActions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;

namespace BlazorBase.Components.IssueReport;

/// <summary>Registration of the issue report.</summary>
public static class IssueReportServiceCollectionExtensions
{
    /// <summary>The <see cref="HeaderActionItem.Id"/> of the issue report header button.</summary>
    public const string HeaderActionId = "report-issue";

    /// <summary>
    /// Registers the issue report dialog, its services, the client diagnostics
    /// (<see cref="ClientDiagnosticsServiceCollectionExtensions.AddBlazorBaseClientDiagnostics"/>) and, unless
    /// <see cref="IssueReportOptions.RegisterHeaderAction"/> is false, a bug icon in the layout header. The
    /// <see cref="IssueReportOptions.Recipient"/> has to be configured. Calling it again only adds configuration.
    /// </summary>
    public static IServiceCollection AddBlazorBaseIssueReporting(this IServiceCollection services, Action<IssueReportOptions>? configure = null)
    {
        var options = services.AddOptions<IssueReportOptions>();
        if (configure is not null)
            options.Configure(configure);

        services.AddBlazorBaseClientDiagnostics();
        services.AddBlazorBaseHeaderActions();
        services.TryAddScoped<IIssueReportBrowser, IssueReportBrowser>();
        services.TryAddScoped<IIssueReportDiagnosticsCollector, IssueReportDiagnosticsCollector>();
        services.TryAddScoped<IIssueReportService, IssueReportService>();

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IssueReportMarker)))
        {
            services.AddSingleton<IssueReportMarker>();
            services.AddHeaderAction(CreateHeaderAction);
        }

        return services;
    }

    private static HeaderActionItem CreateHeaderAction(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<IssueReportOptions>>().Value;
        var localizer = provider.GetRequiredService<IStringLocalizer<BaseIssueReportDialog>>();

        return new HeaderActionItem
        {
            Id = HeaderActionId,
            Icon = new Size20.Bug(),
            Title = localizer["ReportIssue"],
            Order = options.HeaderActionOrder,
            PopupKind = "dialog",
            IsVisible = _ => options.RegisterHeaderAction && !string.IsNullOrWhiteSpace(options.Recipient),
            OnInvoke = services => services.GetRequiredService<IIssueReportService>().OpenAsync(),
        };
    }

    private sealed class IssueReportMarker;
}
