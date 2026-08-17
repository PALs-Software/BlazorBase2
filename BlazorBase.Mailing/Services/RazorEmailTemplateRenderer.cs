using BlazorBase.Mailing.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// Renders Razor components to HTML using the built-in <see cref="HtmlRenderer"/> from
/// <c>Microsoft.AspNetCore.Components.Web</c>. Suitable for transactional email bodies.
/// </summary>
public class RazorEmailTemplateRenderer(HtmlRenderer htmlRenderer) : IEmailTemplateRenderer
{
    #region Injects
    private readonly HtmlRenderer HtmlRenderer = htmlRenderer;
    #endregion

    public Task<string> RenderAsync<TComponent>(IDictionary<string, object?>? parameters = null)
        where TComponent : IComponent
        => RenderAsync<TComponent>(parameters, culture: null);

    public Task<string> RenderAsync<TComponent>(IDictionary<string, object?>? parameters, CultureInfo? culture)
        where TComponent : IComponent
    {
        return HtmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            using var cultureScope = new CultureScope(culture);

            var parameterView = parameters is null
                ? ParameterView.Empty
                : ParameterView.FromDictionary(parameters);

            var output = await HtmlRenderer.RenderComponentAsync<TComponent>(parameterView);
            return output.ToHtmlString();
        });
    }
}
