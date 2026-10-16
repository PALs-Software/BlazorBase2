using System.Net;
using System.Net.Http.Json;
using BlazorBase.Notifications.Models;
using BlazorBase.Notifications.Services;
using BlazorBase.Notifications.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Notifications.Test.Services;

public sealed class HttpNotificationInboxClientTests
{
    private static (HttpNotificationInboxClient Client, StubHttpMessageHandler Handler) CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var handler = new StubHttpMessageHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://portal.test/api/notifications/") };
        return (new HttpNotificationInboxClient(httpClient), handler);
    }

    private static Task<HttpResponseMessage> Json<T>(T value)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) });

    [Fact]
    public async Task GetUnreadAsync_GetsFromUnread()
    {
        var expected = new[] { new InboxNotificationDto(1, "Title", "Body", "Info", DateTime.UtcNow, false) };
        var (client, handler) = CreateClient(_ => Json(expected));

        var notifications = await client.GetUnreadAsync();

        Assert.Single(notifications);
        Assert.Equal("Title", notifications[0].Title);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://portal.test/api/notifications/unread", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetUnreadAsync_NullBody_ReturnsEmpty()
    {
        var (client, _) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create<IReadOnlyList<InboxNotificationDto>?>(null) }));

        var notifications = await client.GetUnreadAsync();

        Assert.Empty(notifications);
    }

    [Fact]
    public async Task GetUnreadCountAsync_GetsFromUnreadCount()
    {
        var (client, handler) = CreateClient(_ => Json(3));

        var count = await client.GetUnreadCountAsync();

        Assert.Equal(3, count);
        Assert.Equal("https://portal.test/api/notifications/unread/count", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task MarkReadAsync_PostsToIdRead()
    {
        var (client, handler) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        await client.MarkReadAsync(42);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://portal.test/api/notifications/42/read", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task DismissAsync_PostsToIdDismiss()
    {
        var (client, handler) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        await client.DismissAsync(42);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://portal.test/api/notifications/42/dismiss", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task MarkReadAsync_Failure_Throws()
    {
        var (client, _) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.MarkReadAsync(1));
    }
}
