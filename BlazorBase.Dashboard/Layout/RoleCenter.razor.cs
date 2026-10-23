using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using BlazorBase.Dashboard.Localization;

namespace BlazorBase.Dashboard;

/// <summary>
/// The start page of a user: a responsive 12-column grid of the registered parts the user may see, in priority order, with a
/// meta row ("updated hh:mm", refresh button) and a silent auto refresh while the tab is visible.
/// </summary>
/// <remarks>
/// Every part sits in its own frame with a loading skeleton, error isolation (one failing part shows a small retry card and
/// never breaks the page) and a collapse for parts that report nothing to show. The role center cascades an
/// <see cref="IDashboardRefreshService"/> to everything inside it. Container queries drive the layout: from a 1100 px wide
/// container the parts use their column widths, narrower ones render every part full width, below 640 px cue groups become two
/// columns.
/// </remarks>
public partial class RoleCenter(
    IStringLocalizer<DashboardResources> localizer,
    IServiceProvider serviceProvider,
    IDashboardPartRegistry registry,
    DashboardSettings settings,
    TimeProvider timeProvider,
    DashboardClock clock,
    DashboardInterop interop) : IAsyncDisposable
{
    #region Injects

    private readonly IStringLocalizer<DashboardResources> Localizer = localizer;
    private readonly IServiceProvider ServiceProvider = serviceProvider;
    private readonly IDashboardPartRegistry Registry = registry;
    private readonly DashboardSettings Settings = settings;
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly DashboardClock Clock = clock;
    private readonly DashboardInterop Interop = interop;

    #endregion

    #region Parameters

    /// <summary>
    /// Content above the meta row, normally a <see cref="HeadlinePart"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? Headline { get; set; }

    /// <summary>
    /// Content at the start of the meta row, before "Updated hh:mm", for example the month the numbers belong to.
    /// </summary>
    [Parameter]
    public RenderFragment? MetaContent { get; set; }

    /// <summary>
    /// Content at the end of the meta row, before the refresh button.
    /// </summary>
    [Parameter]
    public RenderFragment? Tools { get; set; }

    /// <summary>
    /// Overrides the automatic refresh interval configured with <c>AddBlazorBaseDashboard</c> (default five minutes) for this
    /// role center. <see cref="TimeSpan.Zero"/> switches the automatic refresh off.
    /// </summary>
    [Parameter]
    public TimeSpan? AutoRefreshInterval { get; set; }

    /// <summary>
    /// Extra CSS classes for the root element.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    #endregion

    /// <summary>
    /// The refresh service this role center cascades; usable from the page through <c>@ref</c>.
    /// </summary>
    public IDashboardRefreshService RefreshService => Refresh;

    private readonly DashboardRefreshService Refresh = new();

    private readonly CancellationTokenSource DisposalSource = new();

    private IReadOnlyList<DashboardPartRegistration> VisibleParts { get; set; } = [];

    private DateTimeOffset LastUpdated { get; set; }

    private bool IsRefreshing { get; set; }

    private bool IsTabVisible { get; set; } = true;

    private bool IsRefreshDue { get; set; }

    private ITimer? Timer;

    private IAsyncDisposable? VisibilitySubscription;

    private bool IsDisposed;

    private TimeSpan EffectiveInterval => AutoRefreshInterval ?? Settings.AutoRefreshInterval;

    private string UpdatedText => String.Format(Localizer["UpdatedAt"].Value, Clock.ToLocal(LastUpdated).ToString("t", CultureInfo.CurrentCulture));

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        LastUpdated = TimeProvider.GetUtcNow();
        Refresh.BeforeRefreshAllAsync = UpdateVisibleAsync;
        Refresh.RefreshAllCompleted += OnRefreshAllCompleted;
        await UpdateVisibleAsync();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        if (await Clock.EnsureInitializedAsync() && !IsDisposed)
            StateHasChanged();

        IsTabVisible = await Interop.IsTabVisibleAsync();
        VisibilitySubscription = await Interop.ObserveVisibilityAsync(OnVisibilityChangedAsync);

        if (IsDisposed)
            return;

        if (EffectiveInterval > TimeSpan.Zero)
            Timer = TimeProvider.CreateTimer(OnTimerTick, null, EffectiveInterval, EffectiveInterval);
    }

    private async Task RefreshAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        StateHasChanged();

        try
        {
            await Refresh.RefreshAllAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void OnTimerTick(object? state) => _ = InvokeAsync(AutoRefreshAsync);

    private async Task AutoRefreshAsync()
    {
        if (IsDisposed)
            return;

        if (!IsTabVisible)
        {
            IsRefreshDue = true;
            return;
        }

        await RunSilentRefreshAsync();
    }

    private Task OnVisibilityChangedAsync(bool visible) =>
        InvokeAsync(async () =>
        {
            IsTabVisible = visible;

            if (!visible || IsDisposed)
                return;

            var age = TimeProvider.GetUtcNow() - LastUpdated;

            if (IsRefreshDue || (EffectiveInterval > TimeSpan.Zero && age >= EffectiveInterval))
                await RunSilentRefreshAsync();
        });

    private async Task RunSilentRefreshAsync()
    {
        IsRefreshDue = false;
        await Refresh.RefreshAllAsync();
    }

    private void OnRefreshAllCompleted()
    {
        if (IsDisposed)
            return;

        _ = InvokeAsync(() =>
        {
            LastUpdated = TimeProvider.GetUtcNow();
            StateHasChanged();
        });
    }

    private async Task UpdateVisibleAsync()
    {
        var token = DisposalSource.Token;
        var user = await ReadUserAsync();
        var context = new DashboardPartContext(user, ServiceProvider, token);
        var parts = Registry.Parts;
        var decisions = await Task.WhenAll(parts.Select(part => IsVisibleAsync(part, context)));
        var visible = parts.Where((_, index) => decisions[index]).ToList();

        if (visible.Select(part => part.Id).SequenceEqual(VisibleParts.Select(part => part.Id)))
            return;

        VisibleParts = visible;
        await InvokeAsync(StateHasChanged);
    }

    private async Task<System.Security.Claims.ClaimsPrincipal> ReadUserAsync()
    {
        var provider = ServiceProvider.GetService<AuthenticationStateProvider>();

        if (provider is null)
            return new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());

        var state = await provider.GetAuthenticationStateAsync();
        return state.User;
    }

    private static async Task<bool> IsVisibleAsync(DashboardPartRegistration part, DashboardPartContext context)
    {
        try
        {
            return await part.IsVisibleAsync(context);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        DisposalSource.Cancel();
        Timer?.Dispose();
        Refresh.RefreshAllCompleted -= OnRefreshAllCompleted;

        if (VisibilitySubscription is not null)
            await VisibilitySubscription.DisposeAsync();

        DisposalSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
