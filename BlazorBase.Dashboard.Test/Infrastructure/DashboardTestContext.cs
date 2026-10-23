using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// Base bUnit context: English UI, loose JS interop, Fluent UI, localization, the dashboard services and a manual clock.
/// Parts have to be registered before the first render.
/// </summary>
public abstract class DashboardTestContext : BunitContext
{
    protected DashboardTestContext()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en-GB");
        CultureInfo.CurrentCulture = new CultureInfo("en-GB");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
        Services.AddLocalization();
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddBlazorBaseDashboard();
        Authorization = this.AddAuthorization();
        Authorization.SetAuthorized("test@example.com");
    }

    protected ManualTimeProvider Time { get; } = new();

    protected Bunit.TestDoubles.BunitAuthorizationContext Authorization { get; }

    protected BunitJSModuleInterop DashboardModule => JSInterop.SetupModule("./_content/BlazorBase.Dashboard/js/dashboard.js");
}
