using System.Net;
using System.Net.Http.Json;
using BlazorBase.Notifications.Models;
using BlazorBase.Notifications.Services;
using BlazorBase.Notifications.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Notifications.Test.Services;

public sealed class HttpPushSubscriptionClientTests
{
    private static (HttpPushSubscriptionClient Client, StubHttpMessageHandler Handler) CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var handler = new StubHttpMessageHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://portal.test/api/notifications/") };
        return (new HttpPushSubscriptionClient(httpClient), handler);
    }

    private static Task<HttpResponseMessage> Json<T>(T value)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) });

    [Fact]
    public async Task GetPublicKeyAsync_GetsFromPushPublicKey()
    {
        var (client, handler) = CreateClient(_ => Json(new PushPublicKeyResponse("key-123")));

        var key = await client.GetPublicKeyAsync();

        Assert.Equal("key-123", key);
        Assert.Equal("https://portal.test/api/notifications/push/public-key", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetPublicKeyAsync_NullBody_ReturnsEmpty()
    {
        var (client, _) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create<PushPublicKeyResponse?>(null) }));

        var key = await client.GetPublicKeyAsync();

        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public async Task SubscribeAsync_PostsTheSubscriptionAsJson()
    {
        var (client, handler) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        await client.SubscribeAsync(new PushSubscriptionDto("https://push.test/abc", "p256dh", "auth", "agent"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://portal.test/api/notifications/push/subscription", request.RequestUri!.ToString());
        Assert.Contains("\"endpoint\":\"https://push.test/abc\"", handler.RequestBodies[0]);
    }

    [Fact]
    public async Task UnsubscribeAsync_DeletesWithTheEndpointAsAQueryParameter()
    {
        var (client, handler) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        await client.UnsubscribeAsync("https://push.test/abc?x=y");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("https://portal.test/api/notifications/push/subscription?endpoint=https%3A%2F%2Fpush.test%2Fabc%3Fx%3Dy", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task SetEnabledAsync_PutsTheEndpointAndFlag()
    {
        var (client, handler) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        await client.SetEnabledAsync("https://push.test/abc", false);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("https://portal.test/api/notifications/push/subscription/enabled", request.RequestUri!.ToString());
        Assert.Contains("\"enabled\":false", handler.RequestBodies[0]);
    }

    [Fact]
    public async Task SubscribeAsync_Failure_Throws()
    {
        var (client, _) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SubscribeAsync(new PushSubscriptionDto("e", "p", "a", null)));
    }
}
