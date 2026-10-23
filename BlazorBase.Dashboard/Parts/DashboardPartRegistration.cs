using Microsoft.AspNetCore.Components;

namespace BlazorBase.Dashboard;

/// <summary>
/// One part of a role center: any Blazor component that needs no required parameters, plus where, in which order and for
/// whom it is shown.
/// </summary>
public sealed class DashboardPartRegistration
{
    private int ColumnsValue = 12;

    /// <summary>
    /// Stable identifier, unique across the role center. Used to refresh single parts and as the key of the rendered part.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The component to render. It must implement <see cref="IComponent"/>, take no required parameters and may implement
    /// <see cref="IDashboardRefreshable"/>.
    /// </summary>
    public required Type ComponentType { get; init; }

    /// <summary>
    /// Sort key; lower values come first. Parts with the same priority keep their registration order.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Width on the 12-column desktop grid (container 1100 px and wider); values are clamped to 1-12. Narrower containers
    /// render every part full width.
    /// </summary>
    public int Columns
    {
        get => ColumnsValue;
        init => ColumnsValue = Math.Clamp(value, 1, 12);
    }

    /// <summary>
    /// Optional human readable name, used for the loading and error states of the part.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Decides per render whether the signed-in user sees the part. A rule that throws hides the part. Defaults to
    /// <see cref="DashboardVisibility.ForAnyUser"/>.
    /// </summary>
    public Func<DashboardPartContext, ValueTask<bool>> IsVisibleAsync { get; init; } = DashboardVisibility.ForAnyUser();

    /// <summary>
    /// Creates a registration for <typeparamref name="TPart"/>.
    /// </summary>
    public static DashboardPartRegistration For<TPart>(
        string id,
        int priority = 0,
        int columns = 12,
        Func<DashboardPartContext, ValueTask<bool>>? isVisibleAsync = null,
        string? title = null)
        where TPart : IComponent =>
        new()
        {
            Id = id,
            ComponentType = typeof(TPart),
            Priority = priority,
            Columns = columns,
            Title = title,
            IsVisibleAsync = isVisibleAsync ?? DashboardVisibility.ForAnyUser(),
        };
}
