using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Test.Infrastructure;

/// <summary>
/// Drives Fluent components through their parameters instead of through the DOM of the web component behind them,
/// which a bUnit render does not run.
/// </summary>
public static class FluentTestExtensions
{
    /// <summary>Raises the click callback of a button as a user's click would.</summary>
    public static void Click(this IRenderedComponent<FluentButton> button) =>
        button.InvokeAsync(() => button.Instance.OnClick.InvokeAsync(new MouseEventArgs())).GetAwaiter().GetResult();

    /// <summary>Changes the value of an input the way the web component does when the user edits it.</summary>
    public static void SetValue<TValue>(this IRenderedComponent<FluentInputBase<TValue>> input, TValue value) =>
        input.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync(value)).GetAwaiter().GetResult();

    /// <summary>The visible caption of a button: its text without the icon markup.</summary>
    public static string Caption(this IRenderedComponent<FluentButton> button) =>
        string.Concat(button.Nodes.Select(node => node.TextContent)).Trim();
}
