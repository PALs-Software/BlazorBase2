using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using BlazorBase.Dashboard.Localization;

namespace BlazorBase.Dashboard;

/// <summary>
/// The banner on top of a role center: a greeting by time of day with the user's name, up to three rotating insights from the
/// registered <see cref="IHeadlineInsightProvider"/>s and a slot for quick actions.
/// </summary>
/// <remarks>
/// The insights rotate every <see cref="RotationInterval"/>, pause while the pointer or the keyboard focus is inside the
/// banner, stop for good once the user picked one with the dots and do not rotate at all under <c>prefers-reduced-motion</c>.
/// </remarks>
public partial class HeadlinePart(
    IStringLocalizer<DashboardResources> localizer,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    DashboardClock clock,
    DashboardInterop interop) : IDashboardRefreshable, IDisposable
{
    #region Injects

    private readonly IStringLocalizer<DashboardResources> Localizer = localizer;
    private readonly IServiceProvider ServiceProvider = serviceProvider;
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly DashboardClock Clock = clock;
    private readonly DashboardInterop Interop = interop;

    #endregion

    #region Parameters

    /// <summary>
    /// The name to greet. Without it the given name of the signed-in user is used, then the display name; a login that is
    /// an e-mail address is never shown.
    /// </summary>
    [Parameter]
    public string? UserName { get; set; }

    /// <summary>
    /// Quick actions, shown on the right of the banner; on phones a horizontally scrollable row of chips below the text.
    /// Use plain <c>&lt;a&gt;</c> or <c>&lt;button&gt;</c> elements; they are styled as chips.
    /// </summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <summary>
    /// Maximum number of insights shown; default 3.
    /// </summary>
    [Parameter]
    public int MaxInsights { get; set; } = 3;

    /// <summary>
    /// Time between two insights; default 8 seconds. Zero or less switches the rotation off.
    /// </summary>
    [Parameter]
    public TimeSpan RotationInterval { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Extra CSS classes for the banner.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    #endregion

    /// <summary>
    /// The refresh service of the surrounding role center; the headline joins its refresh.
    /// </summary>
    [CascadingParameter]
    public IDashboardRefreshService? RefreshService { get; set; }

    private readonly string GreetingId = $"hl-{Guid.NewGuid():N}";

    private IReadOnlyList<HeadlineInsight> Insights { get; set; } = [];

    private int Index { get; set; }

    private bool IsPaused { get; set; }

    private bool HasPickedInsight { get; set; }

    private bool PrefersReducedMotion { get; set; }

    private string? ResolvedName { get; set; }

    private ITimer? Timer;

    private IDisposable? RefreshRegistration;

    private CancellationTokenSource? LoadSource;

    private bool IsDisposed;

    private string GreetingText
    {
        get
        {
            var key = Clock.LocalNow.Hour switch
            {
                >= 5 and < 12 => "GreetingMorning",
                >= 12 and < 18 => "GreetingAfternoon",
                _ => "GreetingEvening",
            };

            var greeting = Localizer[key].Value;
            var name = UserName ?? ResolvedName;

            return String.IsNullOrWhiteSpace(name) ? greeting : $"{greeting}, {name}";
        }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        RefreshRegistration = RefreshService?.Register("headline", this);
        await ResolveNameAsync();
        await LoadInsightsAsync();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            PrefersReducedMotion = await Interop.PrefersReducedMotionAsync();

            if (await Clock.EnsureInitializedAsync() && !IsDisposed)
                StateHasChanged();
        }

        UpdateTimer();
    }

    /// <summary>
    /// Asks every provider again and shows the new insights. Part of the role center refresh.
    /// </summary>
    public async Task RefreshAsync()
    {
        await LoadInsightsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadInsightsAsync()
    {
        LoadSource?.Cancel();
        LoadSource?.Dispose();
        LoadSource = new CancellationTokenSource();
        var token = LoadSource.Token;

        var providers = ServiceProvider.GetServices<IHeadlineInsightProvider>();
        var collected = new List<HeadlineInsight>();

        foreach (var provider in providers)
        {
            try
            {
                collected.AddRange(await provider.GetInsightsAsync(token));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
            }
        }

        if (token.IsCancellationRequested)
            return;

        var selected = collected.OrderBy(insight => insight.Priority).Take(Math.Max(0, MaxInsights)).ToList();
        var previous = Insights.Count > 0 && Index < Insights.Count ? Insights[Index] : null;

        Insights = selected;
        Index = previous is null ? 0 : Math.Max(0, selected.FindIndex(insight => ReferenceEquals(insight, previous)));
    }

    private async Task ResolveNameAsync()
    {
        if (!String.IsNullOrWhiteSpace(UserName))
            return;

        try
        {
            var provider = ServiceProvider.GetService<AuthenticationStateProvider>();

            if (provider is null)
                return;

            var state = await provider.GetAuthenticationStateAsync();
            ResolvedName = DisplayName(state.User);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }

    internal static string? DisplayName(ClaimsPrincipal user)
    {
        var given = user.FindFirst(ClaimTypes.GivenName)?.Value ?? user.FindFirst("given_name")?.Value;

        if (!String.IsNullOrWhiteSpace(given))
            return given.Trim();

        var name = user.FindFirst("name")?.Value ?? user.Identity?.Name;

        if (String.IsNullOrWhiteSpace(name) || name.Contains('@'))
            return null;

        return name.Trim();
    }

    private bool ShouldRotate => !IsDisposed && !PrefersReducedMotion && !HasPickedInsight && Insights.Count > 1 && RotationInterval > TimeSpan.Zero;

    private void UpdateTimer()
    {
        if (!ShouldRotate)
        {
            Timer?.Dispose();
            Timer = null;
            return;
        }

        Timer ??= TimeProvider.CreateTimer(OnTimerTick, null, RotationInterval, RotationInterval);
    }

    private void OnTimerTick(object? state) => _ = InvokeAsync(Rotate);

    private void Rotate()
    {
        if (IsPaused || !ShouldRotate)
            return;

        Index = (Index + 1) % Insights.Count;
        StateHasChanged();
    }

    private void SetPaused(bool paused) => IsPaused = paused;

    private void ShowInsight(int position)
    {
        HasPickedInsight = true;
        Index = Math.Clamp(position, 0, Math.Max(0, Insights.Count - 1));
        UpdateTimer();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IsDisposed = true;
        Timer?.Dispose();
        RefreshRegistration?.Dispose();
        LoadSource?.Cancel();
        LoadSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
