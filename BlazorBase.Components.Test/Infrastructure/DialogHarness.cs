using BlazorBase.Components.Dialogs;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Test.Infrastructure;

/// <summary>
/// Opens a dialog the way the application does - through the dialog service and a dialog provider - because
/// Fluent's dialog service and dialog instance cannot be replaced by a test double: their contract has internal
/// members. The content component then gets its <see cref="IDialogInstance"/> from the real cascade.
/// </summary>
/// <typeparam name="TDialog">The content component of the dialog.</typeparam>
public sealed class DialogHarness<TDialog>
    where TDialog : ComponentBase
{
    private DialogHarness(IRenderedComponent<FluentDialogProvider> provider, IRenderedComponent<TDialog> content, Task<DialogResult> result)
    {
        Provider = provider;
        Content = content;
        Result = result;
    }

    /// <summary>The provider that renders the dialog.</summary>
    public IRenderedComponent<FluentDialogProvider> Provider { get; }

    /// <summary>The rendered content component, for assertions and for closing the dialog from the inside.</summary>
    public IRenderedComponent<TDialog> Content { get; }

    /// <summary>Completes when the dialog is closed; carries the dialog result.</summary>
    public Task<DialogResult> Result { get; }

    /// <summary>Opens <typeparamref name="TDialog"/> with its content parameter and the default dialog options.</summary>
    public static DialogHarness<TDialog> Open<TContent>(BunitContext context, string title, TContent content)
        where TContent : notnull =>
        Open(context, options =>
        {
            options.Title = title;
            options.Parameters["Content"] = content;
        });

    /// <summary>Opens <typeparamref name="TDialog"/> with options filled in by <paramref name="configure"/>.</summary>
    public static DialogHarness<TDialog> Open(BunitContext context, Action<BaseDialogOptions> configure)
    {
        var provider = context.Render<FluentDialogProvider>();
        var service = context.Services.GetRequiredService<IDialogService>();
        var result = service.ShowBaseDialogAsync<TDialog>(configure);
        var content = provider.FindComponent<TDialog>();

        return new DialogHarness<TDialog>(provider, content, result);
    }
}
