using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// Renders a Razor component to a static HTML string suitable for use as an email body.
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync<TComponent>(IDictionary<string, object?>? parameters = null)
        where TComponent : IComponent;

    /// <summary>
    /// Renders a Razor component to a static HTML string, pinning the current thread's
    /// <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/> to
    /// <paramref name="culture"/> for the duration of the render when it is not <c>null</c>. The
    /// default implementation ignores <paramref name="culture"/> and delegates to the single-parameter
    /// overload, so implementers written before this member was added keep compiling and behaving
    /// exactly as before.
    /// </summary>
    /// <param name="parameters">The component parameters to render with.</param>
    /// <param name="culture">The culture to pin during render, or <c>null</c> to use the ambient culture.</param>
    Task<string> RenderAsync<TComponent>(IDictionary<string, object?>? parameters, CultureInfo? culture)
        where TComponent : IComponent
        => RenderAsync<TComponent>(parameters);
}
