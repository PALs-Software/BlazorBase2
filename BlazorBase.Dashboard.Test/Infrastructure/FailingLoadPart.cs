namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// A <see cref="DashboardPartBase"/> whose load always fails.
/// </summary>
public sealed class FailingLoadPart : DashboardPartBase
{
    protected override Task LoadAsync() => throw new InvalidOperationException("load failed");
}
