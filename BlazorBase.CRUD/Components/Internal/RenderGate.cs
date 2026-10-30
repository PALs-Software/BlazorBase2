using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace BlazorBase.CRUD.Components.Internal;

/// <summary>
/// Renders its content again only when <see cref="Key"/> changed, so that a re-render of the owner caused by something
/// the content does not show (a panel opening, a menu appearing, a spinner) does not rebuild every row of a long list.
/// </summary>
/// <remarks>
/// The owner decides what the content depends on: the key has to change whenever anything the content displays changes.
/// A key that is never equal to the previous one makes the gate transparent.
/// </remarks>
public sealed class RenderGate : ComponentBase
{
    private object? RenderedKey;
    private bool HasRendered;

    /// <summary>
    /// What the content currently depends on; compared with <see cref="object.Equals(object?, object?)"/>.
    /// </summary>
    [Parameter]
    public object? Key { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override bool ShouldRender()
    {
        if (HasRendered && Equals(RenderedKey, Key))
            return false;

        HasRendered = true;
        RenderedKey = Key;
        return true;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
}
