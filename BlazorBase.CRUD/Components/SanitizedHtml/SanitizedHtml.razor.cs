using BlazorBase.CRUD.Sanitization;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components.SanitizedHtml;

/// <summary>
/// Renders an HTML string safely. When <see cref="IHtmlSanitizer"/> is registered in DI the value
/// is sanitized before rendering as a <see cref="MarkupString"/>. When no sanitizer is registered
/// the value is HTML-encoded and rendered as plain text — never as raw markup — so the component
/// is XSS-safe even without a sanitizer.
/// </summary>
public partial class SanitizedHtml : ComponentBase
{
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    /// <summary>The HTML value to display.</summary>
    [Parameter]
    public string? Value { get; set; }

    private MarkupString RenderedContent
    {
        get
        {
            if (string.IsNullOrEmpty(Value))
                return new MarkupString(string.Empty);

            var sanitizer = ServiceProvider.GetService(typeof(IHtmlSanitizer)) as IHtmlSanitizer;

            if (sanitizer is not null)
                return new MarkupString(sanitizer.Sanitize(Value));

            return new MarkupString(System.Net.WebUtility.HtmlEncode(Value));
        }
    }
}
