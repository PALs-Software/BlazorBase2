using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Globalization;

namespace BlazorBase.Mailing.Test.Infrastructure;

/// <summary>
/// Hand-written test component that renders <see cref="CultureInfo.CurrentUICulture"/>'s
/// <see cref="CultureInfo.TwoLetterISOLanguageName"/> as its only content, so a rendered
/// HTML string reveals which culture was ambient on the rendering thread. Written by hand
/// (rather than as a <c>.razor</c> file) to avoid enabling the Razor SDK on the test project.
/// </summary>
public sealed class CultureProbeComponent : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddContent(1, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        builder.CloseElement();
    }
}
