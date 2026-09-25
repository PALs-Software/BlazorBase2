using BlazorBase.Components.Sanitization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.Components.Html;

/// <summary>
/// Renders Markdown as formatted HTML. The conversion itself is safe (<see cref="MarkdownConverter"/>);
/// when an <see cref="IHtmlSanitizer"/> is registered, its policy is applied on top, so a host's allow-list
/// decides what finally reaches the page.
/// </summary>
/// <remarks>
/// A host sanitizer must allow what Markdown produces for the content to keep its structure - typically
/// <c>table</c>, <c>thead</c>, <c>tbody</c>, <c>tr</c>, <c>th</c>, <c>td</c>, <c>pre</c>, <c>code</c>,
/// <c>hr</c>, <c>del</c> and the <c>class</c> attribute (code blocks carry <c>language-*</c>).
/// </remarks>
public partial class MarkdownView(IServiceProvider serviceProvider) : ComponentBase
{
    #region Injects
    private readonly IServiceProvider ServiceProvider = serviceProvider;
    #endregion

    /// <summary>The Markdown to display.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>Additional CSS classes for the wrapper.</summary>
    [Parameter]
    public string? Class { get; set; }

    private MarkupString RenderedContent;

    private string? RenderedValue;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (RenderedValue is not null && string.Equals(RenderedValue, Value, StringComparison.Ordinal))
            return;

        RenderedValue = Value ?? string.Empty;
        var html = MarkdownConverter.ToHtml(Value);
        var sanitizer = ServiceProvider.GetService<IHtmlSanitizer>();
        RenderedContent = new MarkupString(sanitizer is null ? html : sanitizer.Sanitize(html));
    }
}
