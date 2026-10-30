using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Test.Infrastructure;

/// <summary>
/// A dialog service that records what was asked to open and returns an accepted result at once. Fluent's
/// <see cref="IDialogService"/> cannot be replaced by a mock (its contract has internal members), so a test that
/// only needs to know which dialog opened with which options derives from the real service instead.
/// </summary>
/// <param name="serviceProvider">Needs an <c>IJSRuntime</c>, which a mock provides.</param>
public sealed class RecordingDialogService(IServiceProvider serviceProvider) : DialogService(serviceProvider, null)
{
    /// <summary>The dialogs opened so far, in order.</summary>
    public List<(Type Component, DialogOptions Options)> Shown { get; } = [];

    public override Task<DialogResult> ShowDialogAsync(Type componentType, DialogOptions options)
    {
        Shown.Add((componentType, options));
        return Task.FromResult(DialogResult.Ok());
    }
}
