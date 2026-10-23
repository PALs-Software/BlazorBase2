using Microsoft.AspNetCore.Components;

namespace BlazorBase.Dashboard;

/// <summary>
/// The configuration callback of <c>AddBlazorBaseDashboard</c>. Call the method from every module that contributes parts;
/// the registrations add up.
/// </summary>
public sealed class DashboardOptions
{
    internal List<DashboardPartRegistration> Parts { get; } = [];

    internal List<Type> InsightProviderTypes { get; } = [];

    internal TimeSpan? AutoRefreshIntervalValue { get; private set; }

    /// <summary>
    /// How often a visible role center reloads its parts silently. Default five minutes; <see cref="TimeSpan.Zero"/>
    /// switches the automatic refresh off. A role center can override it with its own parameter.
    /// </summary>
    public TimeSpan AutoRefreshInterval
    {
        get => AutoRefreshIntervalValue ?? DashboardSettings.DefaultAutoRefreshInterval;
        set => AutoRefreshIntervalValue = value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    /// <summary>
    /// Registers a part.
    /// </summary>
    public DashboardOptions AddPart(DashboardPartRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        Parts.Add(registration);
        return this;
    }

    /// <summary>
    /// Registers <typeparamref name="TPart"/> as a part.
    /// </summary>
    /// <param name="id">Unique, stable id.</param>
    /// <param name="priority">Sort key; lower comes first.</param>
    /// <param name="columns">Desktop width on the 12-column grid (1-12).</param>
    /// <param name="isVisibleAsync">Visibility rule, for example <see cref="DashboardVisibility.ForRoles"/>; everyone by default.</param>
    /// <param name="title">Optional name for the loading and error states.</param>
    public DashboardOptions AddPart<TPart>(
        string id,
        int priority = 0,
        int columns = 12,
        Func<DashboardPartContext, ValueTask<bool>>? isVisibleAsync = null,
        string? title = null)
        where TPart : IComponent =>
        AddPart(DashboardPartRegistration.For<TPart>(id, priority, columns, isVisibleAsync, title));

    /// <summary>
    /// Registers a scoped <see cref="IHeadlineInsightProvider"/>.
    /// </summary>
    public DashboardOptions AddHeadlineInsightProvider<TProvider>()
        where TProvider : class, IHeadlineInsightProvider
    {
        InsightProviderTypes.Add(typeof(TProvider));
        return this;
    }
}
