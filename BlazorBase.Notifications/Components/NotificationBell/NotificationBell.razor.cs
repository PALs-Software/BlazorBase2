using BlazorBase.Notifications.Interop;
using BlazorBase.Notifications.Models;
using BlazorBase.Notifications.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Notifications.Components.NotificationBell;

/// <summary>
/// An icon button with an unread-count badge and a popover inbox: lists unread notifications,
/// dismisses them one by one or all at once, and keeps itself current by polling every
/// <see cref="NotificationClientOptions.PollInterval"/>, on tab focus, and on the push
/// service-worker's refresh message.
/// </summary>
public partial class NotificationBell(
    INotificationInboxClient inboxClient,
    INotificationRefreshInterop refreshInterop,
    NotificationClientOptions options,
    TimeProvider timeProvider,
    IStringLocalizer<NotificationBell> localizer) : ComponentBase, IAsyncDisposable
{
    #region Injects
    private readonly INotificationInboxClient InboxClient = inboxClient;
    private readonly INotificationRefreshInterop RefreshInterop = refreshInterop;
    private readonly NotificationClientOptions Options = options;
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly IStringLocalizer<NotificationBell> Localizer = localizer;
    #endregion

    /// <summary>Additional CSS classes for the wrapper.</summary>
    [Parameter]
    public string? Class { get; set; }

    private static readonly Icon BellIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size24.Alert();

    private IReadOnlyList<InboxNotificationDto> Notifications { get; set; } = [];

    private int UnreadCount { get; set; }

    private bool IsOpen { get; set; }

    private FluentButton? ToggleButton;

    private ElementReference Panel;

    private bool FocusPanelAfterRender;

    private DotNetObjectReference<NotificationBell>? CallbackReference;

    private ITimer? PollTimer;

    /// <inheritdoc/>
    protected override Task OnInitializedAsync() => RefreshAsync();

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (FocusPanelAfterRender)
        {
            FocusPanelAfterRender = false;
            await Panel.FocusAsync();
        }

        if (!firstRender)
            return;

        CallbackReference = DotNetObjectReference.Create(this);
        await RefreshInterop.RegisterAsync(CallbackReference);
        PollTimer = TimeProvider.CreateTimer(_ => _ = InvokeAsync(RefreshAsync), null, Options.PollInterval, Options.PollInterval);
    }

    /// <summary>Called by the refresh interop on tab focus and on a push service-worker message.</summary>
    [JSInvokable]
    public Task OnRefreshRequested() => InvokeAsync(RefreshAsync);

    private async Task RefreshAsync()
    {
        try
        {
            Notifications = await InboxClient.GetUnreadAsync();
            UnreadCount = await InboxClient.GetUnreadCountAsync();
        }
        catch (HttpRequestException)
        {
            return;
        }

        StateHasChanged();
    }

    private async Task ToggleOpenAsync()
    {
        if (IsOpen)
        {
            await CloseAsync();
            return;
        }

        IsOpen = true;
        FocusPanelAfterRender = true;
    }

    private async Task CloseAsync()
    {
        IsOpen = false;

        if (ToggleButton?.Element is { } toggle)
            await toggle.FocusAsync();
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape" && IsOpen)
            await CloseAsync();
    }

    private async Task DismissAsync(InboxNotificationDto notification)
    {
        await InboxClient.DismissAsync(notification.Id);
        await RefreshAsync();
    }

    private async Task DismissAllAsync()
    {
        await InboxClient.DismissAllAsync();
        await RefreshAsync();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        PollTimer?.Dispose();

        try
        {
            await RefreshInterop.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }

        CallbackReference?.Dispose();
        GC.SuppressFinalize(this);
    }
}
