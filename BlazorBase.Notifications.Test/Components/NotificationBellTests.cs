using BlazorBase.Notifications.Components.NotificationBell;
using BlazorBase.Notifications.Interop;
using BlazorBase.Notifications.Models;
using BlazorBase.Notifications.Services;
using BlazorBase.Notifications.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace BlazorBase.Notifications.Test.Components;

public sealed class NotificationBellTests : BunitContext
{
    private readonly INotificationInboxClient InboxClient = Substitute.For<INotificationInboxClient>();
    private readonly INotificationRefreshInterop RefreshInterop = Substitute.For<INotificationRefreshInterop>();
    private readonly RecordingTimeProvider TimeProvider = new();
    private readonly NotificationClientOptions Options = new() { PollInterval = TimeSpan.FromSeconds(45) };
    private readonly CultureScope Culture = new("en");

    public NotificationBellTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
        Services.AddLocalization();
        Services.AddSingleton(InboxClient);
        Services.AddSingleton(RefreshInterop);
        Services.AddSingleton(Options);
        Services.AddSingleton<TimeProvider>(TimeProvider);

        InboxClient.GetUnreadAsync(Arg.Any<CancellationToken>()).Returns([]);
        InboxClient.GetUnreadCountAsync(Arg.Any<CancellationToken>()).Returns(0);
    }

    protected override void Dispose(bool disposing)
    {
        Culture.Dispose();
        base.Dispose(disposing);
    }

    [Fact]
    public void FirstRender_LoadsTheUnreadListAndCount()
    {
        Render<NotificationBell>();

        InboxClient.Received(1).GetUnreadAsync(Arg.Any<CancellationToken>());
        InboxClient.Received(1).GetUnreadCountAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void UnreadCount_Zero_ShowsNoBadge()
    {
        var cut = Render<NotificationBell>();

        Assert.Empty(cut.FindAll(".notification-bell-badge"));
    }

    [Fact]
    public void UnreadCount_Positive_ShowsTheBadge()
    {
        InboxClient.GetUnreadCountAsync(Arg.Any<CancellationToken>()).Returns(3);

        var cut = Render<NotificationBell>();

        Assert.Equal("3", cut.Find(".notification-bell-badge").TextContent);
    }

    [Fact]
    public void Initially_ThePanelIsClosed()
    {
        var cut = Render<NotificationBell>();

        Assert.Empty(cut.FindAll(".notification-bell-panel"));
    }

    [Fact]
    public void ClickingTheBell_OpensThePanel()
    {
        var cut = Render<NotificationBell>();

        cut.Find(".notification-bell-button").Click();

        Assert.Single(cut.FindAll(".notification-bell-panel"));
    }

    [Fact]
    public void ClickingTheBellTwice_ClosesThePanelAgain()
    {
        var cut = Render<NotificationBell>();

        cut.Find(".notification-bell-button").Click();
        cut.Find(".notification-bell-button").Click();

        Assert.Empty(cut.FindAll(".notification-bell-panel"));
    }

    [Fact]
    public void OpenPanel_NoNotifications_ShowsTheEmptyMessage()
    {
        var cut = Render<NotificationBell>();

        cut.Find(".notification-bell-button").Click();

        Assert.Equal("You have no unread notifications.", cut.Find(".notification-bell-empty").TextContent);
    }

    [Fact]
    public void OpenPanel_WithNotifications_ListsThem()
    {
        InboxClient.GetUnreadAsync(Arg.Any<CancellationToken>())
            .Returns([new InboxNotificationDto(1, "Hello", "<b>World</b>", "Info", DateTime.UtcNow, false)]);

        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();

        Assert.Equal("Hello", cut.Find(".notification-bell-item-title").TextContent);
    }

    [Fact]
    public async Task MarkRead_CallsTheClientAndRefreshes()
    {
        InboxClient.GetUnreadAsync(Arg.Any<CancellationToken>())
            .Returns([new InboxNotificationDto(7, "Hello", "World", "Info", DateTime.UtcNow, false)]);

        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();
        var buttons = cut.FindAll(".notification-bell-item-actions fluent-button");

        await cut.InvokeAsync(() => buttons[0].Click());

        await InboxClient.Received(1).MarkReadAsync(7, Arg.Any<CancellationToken>());
        await InboxClient.Received(2).GetUnreadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dismiss_CallsTheClientAndRefreshes()
    {
        InboxClient.GetUnreadAsync(Arg.Any<CancellationToken>())
            .Returns([new InboxNotificationDto(7, "Hello", "World", "Info", DateTime.UtcNow, false)]);

        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();
        var buttons = cut.FindAll(".notification-bell-item-actions fluent-button");

        await cut.InvokeAsync(() => buttons[1].Click());

        await InboxClient.Received(1).DismissAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void FirstRender_RegistersTheRefreshListener()
    {
        Render<NotificationBell>();

        RefreshInterop.Received(1).RegisterAsync(Arg.Any<DotNetObjectReference<NotificationBell>>());
    }

    [Fact]
    public async Task OnRefreshRequested_RefreshesTheList()
    {
        var cut = Render<NotificationBell>();

        await cut.InvokeAsync(() => cut.Instance.OnRefreshRequested());

        await InboxClient.Received(2).GetUnreadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void FirstRender_StartsPollingAtTheConfiguredInterval()
    {
        Render<NotificationBell>();

        Assert.Equal(1, TimeProvider.CreateTimerCallCount);
        Assert.Equal(TimeSpan.FromSeconds(45), TimeProvider.LastDueTime);
        Assert.Equal(TimeSpan.FromSeconds(45), TimeProvider.LastPeriod);
    }
}
