using System.Net;
using System.Text.Json;
using BlazorBase.Notifications.Server.Configuration;
using BlazorBase.Notifications.Server.Services;
using NSubstitute;
using WebPush;
using Xunit;
using PushSubscription = BlazorBase.Notifications.Server.Entities.PushSubscription;
using WebPushSubscription = WebPush.PushSubscription;

namespace BlazorBase.Notifications.Server.Test.Services;

public sealed class WebPushSenderTests
{
    private readonly IWebPushClient WebPushClient = Substitute.For<IWebPushClient>();

    private static WebPushOptions Options(int timeToLiveSeconds = 2419200, PushUrgency urgency = PushUrgency.Normal) => new()
    {
        Subject = "mailto:support@example.test",
        PublicKey = "test-public-key",
        PrivateKey = "test-private-key",
        TimeToLiveSeconds = timeToLiveSeconds,
        Urgency = urgency,
    };

    private static PushSubscription Subscription() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Endpoint = "https://push.example.test/abc",
        P256dh = "p256dh-value",
        Auth = "auth-value",
        Enabled = true,
    };

    private WebPushSender CreateSender(WebPushOptions? options = null) =>
        new(WebPushClient, Microsoft.Extensions.Options.Options.Create(options ?? Options()));

    [Fact]
    public async Task SendAsync_ReturnsSent_WhenTheClientAccepts()
    {
        WebPushClient.SendNotificationAsync(Arg.Any<WebPushSubscription>(), Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await CreateSender().SendAsync(Subscription(), new PushPayload("Title", "Body"), CancellationToken.None);

        Assert.Equal(PushSendResult.Sent, result);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task SendAsync_ReturnsSubscriptionGone_WhenThePushServiceAnswers404Or410(HttpStatusCode statusCode)
    {
        var exception = new WebPushException("gone", new WebPushSubscription("e", "p", "a"), new HttpResponseMessage(statusCode));
        WebPushClient.SendNotificationAsync(Arg.Any<WebPushSubscription>(), Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(exception));

        var result = await CreateSender().SendAsync(Subscription(), new PushPayload("Title", "Body"), CancellationToken.None);

        Assert.Equal(PushSendResult.SubscriptionGone, result);
    }

    [Fact]
    public async Task SendAsync_ReturnsFailed_ForAnyOtherWebPushStatusCode()
    {
        var exception = new WebPushException("server error", new WebPushSubscription("e", "p", "a"), new HttpResponseMessage(HttpStatusCode.InternalServerError));
        WebPushClient.SendNotificationAsync(Arg.Any<WebPushSubscription>(), Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(exception));

        var result = await CreateSender().SendAsync(Subscription(), new PushPayload("Title", "Body"), CancellationToken.None);

        Assert.Equal(PushSendResult.Failed, result);
    }

    [Fact]
    public async Task SendAsync_ReturnsFailed_WhenTheHttpRequestItselfFails()
    {
        WebPushClient.SendNotificationAsync(Arg.Any<WebPushSubscription>(), Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("network down")));

        var result = await CreateSender().SendAsync(Subscription(), new PushPayload("Title", "Body"), CancellationToken.None);

        Assert.Equal(PushSendResult.Failed, result);
    }

    [Fact]
    public async Task SendAsync_SerializesThePayload_AsCamelCaseJson_OmittingNullOptionalFields()
    {
        string? capturedJson = null;
        WebPushClient.SendNotificationAsync(Arg.Any<WebPushSubscription>(), Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedJson = callInfo.ArgAt<string>(1);
                return Task.CompletedTask;
            });

        await CreateSender().SendAsync(Subscription(), new PushPayload("New message", "Something happened"), CancellationToken.None);

        Assert.NotNull(capturedJson);
        using var document = JsonDocument.Parse(capturedJson);
        var root = document.RootElement;
        Assert.Equal("New message", root.GetProperty("title").GetString());
        Assert.Equal("Something happened", root.GetProperty("body").GetString());
        Assert.False(root.TryGetProperty("url", out _));
        Assert.False(root.TryGetProperty("tag", out _));
    }

    [Fact]
    public async Task SendAsync_SerializesThePayload_IncludingUrlAndTag_WhenProvided()
    {
        string? capturedJson = null;
        WebPushClient.SendNotificationAsync(Arg.Any<WebPushSubscription>(), Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedJson = callInfo.ArgAt<string>(1);
                return Task.CompletedTask;
            });

        await CreateSender().SendAsync(Subscription(), new PushPayload("Title", "Body", "/dashboard", "dashboard-update"), CancellationToken.None);

        Assert.NotNull(capturedJson);
        using var document = JsonDocument.Parse(capturedJson);
        var root = document.RootElement;
        Assert.Equal("/dashboard", root.GetProperty("url").GetString());
        Assert.Equal("dashboard-update", root.GetProperty("tag").GetString());
    }

    [Fact]
    public async Task SendAsync_PassesTheConfiguredTimeToLive_AndUrgencyHeader()
    {
        Dictionary<string, object>? capturedOptions = null;
        WebPushClient.SendNotificationAsync(Arg.Any<WebPushSubscription>(), Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedOptions = callInfo.ArgAt<Dictionary<string, object>>(2);
                return Task.CompletedTask;
            });

        await CreateSender(Options(timeToLiveSeconds: 600, urgency: PushUrgency.High)).SendAsync(Subscription(), new PushPayload("Title", "Body"), CancellationToken.None);

        Assert.NotNull(capturedOptions);
        Assert.Equal(600, capturedOptions!["TTL"]);
        var headers = Assert.IsType<Dictionary<string, object>>(capturedOptions["headers"]);
        Assert.Equal("high", headers["Urgency"]);
    }
}
