using BlazorBase.Notifications.Components.NotificationBell;
using BlazorBase.Notifications.Interop;
using BlazorBase.Notifications.Models;
using BlazorBase.Notifications.Services;
using BlazorBase.Notifications.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;
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
    public void OpenPanel_MarksTheButtonExpandedAndRendersABackdrop()
    {
        var cut = Render<NotificationBell>();

        cut.Find(".notification-bell-button").Click();

        Assert.Equal("true", cut.Find(".notification-bell-button").GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll(".notification-bell-backdrop"));
    }

    [Fact]
    public void ClickingTheBackdrop_ClosesThePanel()
    {
        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();

        cut.Find(".notification-bell-backdrop").Click();

        Assert.Empty(cut.FindAll(".notification-bell-panel"));
        Assert.Equal("false", cut.Find(".notification-bell-button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void PressingEscape_ClosesThePanel()
    {
        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();

        cut.Find(".notification-bell").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".notification-bell-panel"));
    }

    [Fact]
    public void PressingAnotherKey_KeepsThePanelOpen()
    {
        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();

        cut.Find(".notification-bell").KeyDown(new KeyboardEventArgs { Key = "Tab" });

        Assert.Single(cut.FindAll(".notification-bell-panel"));
    }

    [Fact]
    public void OpenPanel_FormatsTheTimestampWithoutSeconds()
    {
        var created = new DateTime(2026, 3, 4, 13, 45, 59, DateTimeKind.Utc);
        InboxClient.GetUnreadAsync(Arg.Any<CancellationToken>())
            .Returns([new InboxNotificationDto(1, "Hello", "World", "Info", created, false)]);

        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();

        Assert.Equal(created.ToLocalTime().ToString("g"), cut.Find(".notification-bell-item-time").TextContent);
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
    public async Task Dismiss_CallsTheClientAndRefreshes()
    {
        InboxClient.GetUnreadAsync(Arg.Any<CancellationToken>())
            .Returns([new InboxNotificationDto(7, "Hello", "World", "Info", DateTime.UtcNow, false)]);

        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();

        await cut.InvokeAsync(() => cut.Find(".notification-bell-item-dismiss").Click());

        await InboxClient.Received(1).DismissAsync(7, Arg.Any<CancellationToken>());
        await InboxClient.Received(2).GetUnreadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void EveryItem_OffersASingleDismissButton_AndNoMarkReadButton()
    {
        InboxClient.GetUnreadAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new InboxNotificationDto(1, "First", "World", "Info", DateTime.UtcNow, false),
            new InboxNotificationDto(2, "Second", "World", "Info", DateTime.UtcNow, false)
        ]);

        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();

        Assert.Equal(2, cut.FindAll(".notification-bell-item-dismiss").Count);
        Assert.Empty(cut.FindAll(".notification-bell-item-actions"));
    }

    [Fact]
    public async Task DismissAll_CallsTheClientOnceAndRefreshes()
    {
        InboxClient.GetUnreadAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new InboxNotificationDto(1, "First", "World", "Info", DateTime.UtcNow, false),
            new InboxNotificationDto(2, "Second", "World", "Info", DateTime.UtcNow, false)
        ]);

        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();

        await cut.InvokeAsync(() => cut.Find(".notification-bell-dismiss-all").Click());

        await InboxClient.Received(1).DismissAllAsync(Arg.Any<CancellationToken>());
        await InboxClient.DidNotReceive().DismissAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await InboxClient.Received(2).GetUnreadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void WithoutNotifications_ThePanelShowsTheEmptyTextAndNoDismissAll()
    {
        var cut = Render<NotificationBell>();
        cut.Find(".notification-bell-button").Click();

        Assert.NotEmpty(cut.FindAll(".notification-bell-empty"));
        Assert.Empty(cut.FindAll(".notification-bell-dismiss-all"));
    }

    [Fact]
    public void FirstRender_RegistersTheRefreshListener()
    {
        Render<NotificationBell>();

        RefreshInterop.Received(1).RegisterAsync(Arg.Any<DotNetObjectReference<NotificationBell>>());
    }

    [Fact]
    public void FirstRender_WhenTheServerIsUnreachable_KeepsTheBellRenderedWithoutABadge()
    {
        InboxClient.GetUnreadAsync(Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<InboxNotificationDto>>>(_ => throw new HttpRequestException("offline"));

        var cut = Render<NotificationBell>();

        Assert.Single(cut.FindAll(".notification-bell-button"));
        Assert.Empty(cut.FindAll(".notification-bell-badge"));
    }

    [Fact]
    public async Task OnRefreshRequested_WhenTheServerIsUnreachable_KeepsTheLastKnownCount()
    {
        InboxClient.GetUnreadCountAsync(Arg.Any<CancellationToken>()).Returns(3);
        var cut = Render<NotificationBell>();
        InboxClient.GetUnreadAsync(Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<InboxNotificationDto>>>(_ => throw new HttpRequestException("offline"));

        await cut.InvokeAsync(() => cut.Instance.OnRefreshRequested());

        Assert.Equal("3", cut.Find(".notification-bell-badge").TextContent.Trim());
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
