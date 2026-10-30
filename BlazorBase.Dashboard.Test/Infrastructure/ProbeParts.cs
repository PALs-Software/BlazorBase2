using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// A part that throws while initializing as long as <see cref="PartProbe.ShouldFail"/> is set.
/// </summary>
public sealed class FailingPart(PartProbe probe) : ComponentBase
{
    protected override void OnInitialized()
    {
        if (probe.ShouldFail)
            throw new InvalidOperationException("The part is broken.");
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "recovered-part");
        builder.AddContent(2, "Recovered");
        builder.CloseElement();
    }
}

/// <summary>
/// A part that counts refreshes and can be told to fail on one.
/// </summary>
public sealed class RefreshablePart(PartProbe probe) : ComponentBase, IDashboardRefreshable
{
    public Task RefreshAsync()
    {
        probe.Refreshes++;

        if (probe.ShouldFailOnRefresh)
            throw new InvalidOperationException("Refresh failed.");

        return Task.CompletedTask;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "refreshable-part");
        builder.AddContent(2, "Refreshable");
        builder.CloseElement();
    }
}

/// <summary>
/// A part that collapses itself while <see cref="PartProbe.ReportsEmpty"/> is set.
/// </summary>
public sealed class EmptyingPart(PartProbe probe) : ComponentBase
{
    [CascadingParameter]
    public IDashboardPartHost? Host { get; set; }

    protected override void OnInitialized() => Host?.ReportEmpty(probe.ReportsEmpty);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "emptying-part");
        builder.AddContent(2, "Content");
        builder.CloseElement();
    }
}

/// <summary>
/// A <see cref="DashboardPartBase"/> that waits for <see cref="PartProbe.LoadGate"/>.
/// </summary>
public sealed class LoadingPart(PartProbe probe) : DashboardPartBase
{
    public bool IsCancelled => DisposalToken.IsCancellationRequested;

    protected override async Task LoadAsync()
    {
        probe.Loads++;

        if (probe.LoadGate is { } gate)
            await gate.Task;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "loading-part");
        builder.AddContent(2, $"Loaded {probe.Loads}");
        builder.CloseElement();
    }
}

/// <summary>
/// A part that asks the cascaded refresh service to refresh another part.
/// </summary>
public sealed class RefreshRequestingPart : ComponentBase
{
    [CascadingParameter]
    public IDashboardRefreshService? Service { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "class", "request-refresh");
        builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => Service!.RefreshAsync("refreshable")));
        builder.AddContent(3, "Refresh the other part");
        builder.CloseElement();
    }
}
