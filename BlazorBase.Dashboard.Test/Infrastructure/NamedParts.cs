using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// A part that renders its own type name; subclasses give registrations distinct types.
/// </summary>
public abstract class NamedPart : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "named-part");
        builder.AddContent(2, GetType().Name);
        builder.CloseElement();
    }
}

public sealed class PartA : NamedPart;

public sealed class PartB : NamedPart;

public sealed class PartC : NamedPart;

public sealed class PartD : NamedPart;
