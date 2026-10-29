using BlazorBase.CRUD.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Test.Infrastructure;

/// <summary>
/// A dialog service for tests that only need to know which dialog opened with which content. Fluent's
/// <see cref="IDialogService"/> cannot be replaced by a mock (its contract has internal members), so the tests
/// derive from the real service instead. A dialog stays open until its <see cref="RecordedDialog.Completion"/> is
/// completed, unless <see cref="ImmediateResult"/> is set.
/// </summary>
public sealed class TestDialogService(IServiceProvider serviceProvider) : DialogService(serviceProvider, null)
{
    /// <summary>The dialogs opened so far, in order.</summary>
    public List<RecordedDialog> Shown { get; } = [];

    /// <summary>When set, every dialog closes at once with this result.</summary>
    public DialogResult? ImmediateResult { get; set; }

    /// <summary>Registers a new service in the context and returns it.</summary>
    public static TestDialogService Register(BunitContext context, DialogResult? immediateResult = null)
    {
        var provider = new ServiceCollection().AddSingleton(context.JSInterop.JSRuntime).BuildServiceProvider();
        var service = new TestDialogService(provider) { ImmediateResult = immediateResult };
        context.Services.AddSingleton<IDialogService>(service);
        return service;
    }

    public override Task<DialogResult> ShowDialogAsync(Type componentType, DialogOptions options)
    {
        var completion = new TaskCompletionSource<DialogResult>();
        var recorded = new RecordedDialog(componentType, options, completion);
        Shown.Add(recorded);

        if (recorded.Content is IAttachableDialogData data)
        {
            data.AttachAsync(() =>
            {
                completion.TrySetResult(DialogResult.Cancel());
                return Task.CompletedTask;
            });
        }

        if (ImmediateResult is { } result)
            completion.TrySetResult(result);

        return completion.Task;
    }

    /// <summary>The card data of the dialogs opened for <typeparamref name="TModel"/>.</summary>
    public List<BaseDialogData<TModel>> CardData<TModel>()
        where TModel : class =>
        [.. Shown.Select(dialog => dialog.Content).OfType<BaseDialogData<TModel>>()];
}

/// <summary>A dialog the <see cref="TestDialogService"/> was asked to open.</summary>
/// <param name="Component">The content component of the dialog.</param>
/// <param name="Options">The options it was opened with.</param>
/// <param name="Completion">Completing it closes the dialog.</param>
public sealed record RecordedDialog(Type Component, DialogOptions Options, TaskCompletionSource<DialogResult> Completion)
{
    /// <summary>The <c>Content</c> parameter of the dialog component.</summary>
    public object? Content => Options.Parameters.TryGetValue("Content", out var content) ? content : null;
}
