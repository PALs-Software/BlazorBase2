using BlazorBase.Mailing.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace BlazorBase.Mailing.Test.Infrastructure;

/// <summary>
/// Renders a Razor component to HTML through a real <see cref="HtmlRenderer"/> and
/// <see cref="RazorEmailTemplateRenderer"/>, exactly as a host would after
/// <c>AddBlazorBaseMailing</c>, so the rendering path is exercised end-to-end.
/// </summary>
public static class EmailRenderHarness
{
    public static async Task<string> RenderAsync<TComponent>(IDictionary<string, object?>? parameters = null)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();

        await using var provider = services.BuildServiceProvider();
        await using var htmlRenderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var renderer = new RazorEmailTemplateRenderer(htmlRenderer);
        return await renderer.RenderAsync<TComponent>(parameters);
    }

    /// <summary>
    /// Renders a Razor component to HTML the same way as the single-parameter overload, but through
    /// the culture-aware two-parameter <see cref="RazorEmailTemplateRenderer"/> overload, so tests can
    /// exercise culture pinning end-to-end.
    /// </summary>
    public static async Task<string> RenderAsync<TComponent>(IDictionary<string, object?>? parameters, CultureInfo? culture)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();

        await using var provider = services.BuildServiceProvider();
        await using var htmlRenderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var renderer = new RazorEmailTemplateRenderer(htmlRenderer);
        return await renderer.RenderAsync<TComponent>(parameters, culture);
    }
}
