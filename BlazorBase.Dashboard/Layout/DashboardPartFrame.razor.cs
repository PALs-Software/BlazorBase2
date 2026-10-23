using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using BlazorBase.Dashboard.Localization;

namespace BlazorBase.Dashboard;

/// <summary>
/// The frame around one part of a role center: the grid cell, the loading skeleton, the collapse for an empty part and the
/// retry card. Used by <see cref="RoleCenter"/>; not meant to be placed by hand.
/// </summary>
public partial class DashboardPartFrame(IStringLocalizer<DashboardResources> localizer, IServiceProvider serviceProvider) : IDashboardPartHost, IDisposable
{
    #region Injects

    private readonly IStringLocalizer<DashboardResources> Localizer = localizer;
    private readonly IServiceProvider ServiceProvider = serviceProvider;

    #endregion

    #region Parameters

    /// <summary>
    /// The part to frame.
    /// </summary>
    [Parameter, EditorRequired]
    public DashboardPartRegistration Registration { get; set; } = default!;

    /// <summary>
    /// The refresh service of the role center the frame registers its part with.
    /// </summary>
    [Parameter, EditorRequired]
    public DashboardRefreshService Refresh { get; set; } = default!;

    #endregion

    /// <inheritdoc />
    public string PartId => Registration.Id;

    private DynamicComponent? Dynamic { get; set; }

    private Exception? RefreshFailure { get; set; }

    private bool BoundaryFailed { get; set; }

    private bool IsLoading { get; set; }

    private bool IsEmpty { get; set; }

    private int Generation { get; set; }

    private bool IsDisposed;

    private IDisposable? RefreshRegistration;

    private bool HasFailed => RefreshFailure is not null || BoundaryFailed;

    private bool IsCollapsed => IsEmpty && !HasFailed;

    /// <inheritdoc />
    protected override void OnInitialized() =>
        RefreshRegistration = Refresh.Register(Registration.Id, () => Dynamic?.Instance as IDashboardRefreshable, OnRefreshFailed);

    /// <inheritdoc />
    public void ReportLoading() => Change(() => IsLoading, value => IsLoading = value, true);

    /// <inheritdoc />
    public void ReportLoaded() => Change(() => IsLoading, value => IsLoading = value, false);

    /// <inheritdoc />
    public void ReportEmpty(bool isEmpty = true) => Change(() => IsEmpty, value => IsEmpty = value, isEmpty);

    private void Change(Func<bool> get, Action<bool> set, bool value)
    {
        if (IsDisposed || get() == value)
            return;

        set(value);
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnBoundaryFailed(Exception exception)
    {
        BoundaryFailed = true;
        IsLoading = false;
    }

    private void OnRefreshFailed(Exception exception)
    {
        ServiceProvider.GetService<ILogger<DashboardPartFrame>>()?.LogError(exception, "Refreshing the dashboard part {PartId} failed.", Registration.Id);

        _ = InvokeAsync(() =>
        {
            RefreshFailure = exception;
            IsLoading = false;
            StateHasChanged();
        });
    }

    private void Retry()
    {
        RefreshFailure = null;
        BoundaryFailed = false;
        IsLoading = false;
        IsEmpty = false;
        Generation++;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IsDisposed = true;
        RefreshRegistration?.Dispose();
        GC.SuppressFinalize(this);
    }
}
