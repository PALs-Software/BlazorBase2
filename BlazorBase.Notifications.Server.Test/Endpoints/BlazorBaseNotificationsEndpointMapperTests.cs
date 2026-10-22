using System.Net;
using System.Net.Http.Json;
using BlazorBase.Notifications.Models;
using BlazorBase.Notifications.Server.Endpoints;
using BlazorBase.Notifications.Server.Entities;
using BlazorBase.Notifications.Server.Services;
using BlazorBase.Notifications.Server.Test.Infrastructure;
using BlazorBase.Notifications.Server.Test.TestDoubles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.Notifications.Server.Test.Endpoints;

/// <summary>
/// End-to-end coverage for <c>MapBlazorBaseNotifications</c> through a real <see cref="TestServer"/>:
/// every endpoint rejects an unauthenticated caller, the acting user id always comes from the
/// <see cref="TestUserAuthenticationHandler"/>-issued claims principal, and a caller can never read,
/// remove or toggle another user's push subscription.
/// </summary>
public sealed class BlazorBaseNotificationsEndpointMapperTests
{
    private sealed class NotificationsTestHost : IDisposable
    {
        public required IHost Host { get; init; }
        public required FakeNotificationInboxStore InboxStore { get; init; }
        public required SqliteConnection Connection { get; init; }

        public HttpClient CreateClient() => Host.GetTestClient();

        public async Task<IReadOnlyList<PushSubscription>> GetSubscriptionsForUserAsync(Guid userId)
        {
            using var scope = Host.Services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>();
            return await store.GetForUserAsync(userId, CancellationToken.None);
        }

        public void Dispose()
        {
            Host.Dispose();
            Connection.Dispose();
        }
    }

    private static async Task<NotificationsTestHost> StartHostAsync(INotificationUserIdResolver? userIdResolver = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlazorBaseNotifications:WebPush:Subject"] = "mailto:test@example.test",
                ["BlazorBaseNotifications:WebPush:PublicKey"] = "test-public-key",
                ["BlazorBaseNotifications:WebPush:PrivateKey"] = "test-private-key",
            })
            .Build();

        var inboxStore = new FakeNotificationInboxStore();

        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddDbContext<TestNotificationsDbContext>(options => options.UseSqlite(connection));
                    if (userIdResolver is not null)
                        services.AddSingleton(userIdResolver);
                    services.AddBlazorBaseNotificationsServer<TestNotificationsDbContext>(configuration);
                    services.AddSingleton<INotificationInboxStore>(inboxStore);
                    services.AddAuthentication(TestUserAuthenticationHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestUserAuthenticationHandler>(TestUserAuthenticationHandler.SchemeName, _ => { });
                    services.AddAuthorization();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapBlazorBaseNotifications());
                });
            })
            .StartAsync();

        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestNotificationsDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        return new NotificationsTestHost { Host = host, InboxStore = inboxStore, Connection = connection };
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, Guid? userId, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };

        if (userId is not null)
            request.Headers.Add(TestUserAuthenticationHandler.UserIdHeader, userId.Value.ToString());

        return client.SendAsync(request);
    }

    public static TheoryData<HttpMethod, string> EveryEndpoint() => new()
    {
        { HttpMethod.Get, "/api/notifications/push/public-key" },
        { HttpMethod.Post, "/api/notifications/push/subscription" },
        { HttpMethod.Delete, "/api/notifications/push/subscription?endpoint=https://push.example.test/x" },
        { HttpMethod.Put, "/api/notifications/push/subscription/enabled" },
        { HttpMethod.Get, "/api/notifications/unread" },
        { HttpMethod.Get, "/api/notifications/unread/count" },
        { HttpMethod.Post, "/api/notifications/1/read" },
        { HttpMethod.Post, "/api/notifications/1/dismiss" },
    };

    [Theory]
    [MemberData(nameof(EveryEndpoint))]
    public async Task EveryEndpoint_RejectsAnUnauthenticatedCaller(HttpMethod method, string path)
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();

        var response = await SendAsync(client, method, path, userId: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPublicKey_ReturnsTheConfiguredVapidPublicKey_ForAnAuthenticatedCaller()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();

        var response = await SendAsync(client, HttpMethod.Get, "/api/notifications/push/public-key", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PushPublicKeyResponse>();
        Assert.Equal("test-public-key", body?.PublicKey);
    }

    [Fact]
    public async Task SubscribeAsync_CreatesASubscription_ScopedToTheCallingUser()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var userId = Guid.NewGuid();
        var dto = new PushSubscriptionDto("https://push.example.test/mine", "p256dh", "auth", "Chrome");

        var response = await SendAsync(client, HttpMethod.Post, "/api/notifications/push/subscription", userId, JsonContent.Create(dto));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var subscriptions = await testHost.GetSubscriptionsForUserAsync(userId);
        var subscription = Assert.Single(subscriptions);
        Assert.Equal("https://push.example.test/mine", subscription.Endpoint);
        Assert.Equal("p256dh", subscription.P256dh);
        Assert.Equal("auth", subscription.Auth);
        Assert.Equal("Chrome", subscription.UserAgent);
    }

    [Fact]
    public async Task SubscribeAsync_RejectsAMissingEndpoint()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var dto = new PushSubscriptionDto("", "p256dh", "auth", null);

        var response = await SendAsync(client, HttpMethod.Post, "/api/notifications/push/subscription", Guid.NewGuid(), JsonContent.Create(dto));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesTheCallersOwnSubscription()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var userId = Guid.NewGuid();
        await SendAsync(client, HttpMethod.Post, "/api/notifications/push/subscription", userId,
            JsonContent.Create(new PushSubscriptionDto("https://push.example.test/own", "p256dh", "auth", null)));

        var response = await SendAsync(client, HttpMethod.Delete, "/api/notifications/push/subscription?endpoint=https://push.example.test/own", userId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await testHost.GetSubscriptionsForUserAsync(userId));
    }

    [Fact]
    public async Task UnsubscribeAsync_ReturnsNotFound_ForAnUnknownEndpoint()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();

        var response = await SendAsync(client, HttpMethod.Delete, "/api/notifications/push/subscription?endpoint=https://push.example.test/unknown", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnsubscribeAsync_CannotRemoveAnotherUsersSubscription()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        await SendAsync(client, HttpMethod.Post, "/api/notifications/push/subscription", ownerId,
            JsonContent.Create(new PushSubscriptionDto("https://push.example.test/protected", "p256dh", "auth", null)));

        var response = await SendAsync(client, HttpMethod.Delete, "/api/notifications/push/subscription?endpoint=https://push.example.test/protected", attackerId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(await testHost.GetSubscriptionsForUserAsync(ownerId));
    }

    [Fact]
    public async Task SetSubscriptionEnabledAsync_TogglesTheCallersOwnSubscription()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var userId = Guid.NewGuid();
        await SendAsync(client, HttpMethod.Post, "/api/notifications/push/subscription", userId,
            JsonContent.Create(new PushSubscriptionDto("https://push.example.test/toggle", "p256dh", "auth", null)));

        var response = await SendAsync(client, HttpMethod.Put, "/api/notifications/push/subscription/enabled", userId,
            JsonContent.Create(new SetPushSubscriptionEnabledRequest("https://push.example.test/toggle", false)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var subscription = Assert.Single(await testHost.GetSubscriptionsForUserAsync(userId));
        Assert.False(subscription.Enabled);
    }

    [Fact]
    public async Task SetSubscriptionEnabledAsync_CannotToggleAnotherUsersSubscription()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        await SendAsync(client, HttpMethod.Post, "/api/notifications/push/subscription", ownerId,
            JsonContent.Create(new PushSubscriptionDto("https://push.example.test/protected-toggle", "p256dh", "auth", null)));

        var response = await SendAsync(client, HttpMethod.Put, "/api/notifications/push/subscription/enabled", attackerId,
            JsonContent.Create(new SetPushSubscriptionEnabledRequest("https://push.example.test/protected-toggle", false)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var subscription = Assert.Single(await testHost.GetSubscriptionsForUserAsync(ownerId));
        Assert.True(subscription.Enabled);
    }

    [Fact]
    public async Task GetUnreadAsync_ReturnsTheInboxStoreResult_ForTheCallingUser()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        testHost.InboxStore.UnreadByUser[userId] = [new InboxNotificationDto(1, "Title", "Message", "Info", DateTime.UtcNow, false)];
        testHost.InboxStore.UnreadByUser[otherUserId] = [new InboxNotificationDto(2, "Other", "Message", "Info", DateTime.UtcNow, false)];

        var response = await SendAsync(client, HttpMethod.Get, "/api/notifications/unread", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<InboxNotificationDto>>();
        var notification = Assert.Single(body!);
        Assert.Equal(1, notification.Id);
        Assert.Equal([userId], testHost.InboxStore.GetUnreadCalls);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsTheInboxStoreCount_ForTheCallingUser()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var userId = Guid.NewGuid();
        testHost.InboxStore.UnreadByUser[userId] =
        [
            new InboxNotificationDto(1, "Title", "Message", "Info", DateTime.UtcNow, false),
            new InboxNotificationDto(2, "Title", "Message", "Info", DateTime.UtcNow, false),
        ];

        var response = await SendAsync(client, HttpMethod.Get, "/api/notifications/unread/count", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var count = await response.Content.ReadFromJsonAsync<int>();
        Assert.Equal(2, count);
        Assert.Equal([userId], testHost.InboxStore.GetUnreadCountCalls);
    }

    [Fact]
    public async Task MarkReadAsync_CallsTheInboxStore_WithTheCallingUserAndTheGivenId()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var userId = Guid.NewGuid();

        var response = await SendAsync(client, HttpMethod.Post, "/api/notifications/42/read", userId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([(userId, 42L)], testHost.InboxStore.MarkReadCalls);
    }

    [Fact]
    public async Task DismissAsync_CallsTheInboxStore_WithTheCallingUserAndTheGivenId()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var userId = Guid.NewGuid();

        var response = await SendAsync(client, HttpMethod.Post, "/api/notifications/42/dismiss", userId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([(userId, 42L)], testHost.InboxStore.DismissCalls);
    }

    [Fact]
    public async Task DismissAllAsync_CallsTheInboxStore_WithTheCallingUser()
    {
        using var testHost = await StartHostAsync();
        using var client = testHost.CreateClient();
        var userId = Guid.NewGuid();

        var response = await SendAsync(client, HttpMethod.Post, "/api/notifications/dismiss-all", userId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([userId], testHost.InboxStore.DismissAllCalls);
    }

    public static TheoryData<HttpMethod, string, HttpContent?> UserScopedEndpoints() => new()
    {
        { HttpMethod.Post, "/api/notifications/push/subscription", JsonContent.Create(new PushSubscriptionDto("https://push.example.test/unresolved", "p256dh", "auth", null)) },
        { HttpMethod.Delete, "/api/notifications/push/subscription?endpoint=https://push.example.test/x", null },
        { HttpMethod.Put, "/api/notifications/push/subscription/enabled", JsonContent.Create(new SetPushSubscriptionEnabledRequest("https://push.example.test/x", false)) },
        { HttpMethod.Get, "/api/notifications/unread", null },
        { HttpMethod.Get, "/api/notifications/unread/count", null },
        { HttpMethod.Post, "/api/notifications/1/read", null },
        { HttpMethod.Post, "/api/notifications/1/dismiss", null },
        { HttpMethod.Post, "/api/notifications/dismiss-all", null },
    };

    [Theory]
    [MemberData(nameof(UserScopedEndpoints))]
    public async Task EveryUserScopedEndpoint_RejectsTheCaller_WhenTheRegisteredResolverCannotResolveAnId(HttpMethod method, string path, HttpContent? content)
    {
        var resolver = new MappingNotificationUserIdResolver(new Dictionary<Guid, Guid>());
        using var testHost = await StartHostAsync(resolver);
        using var client = testHost.CreateClient();

        var response = await SendAsync(client, method, path, Guid.NewGuid(), content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubscribeAsync_StoresTheSubscription_UnderTheResolverSUserId_NotTheIdentityId()
    {
        var identityUserId = Guid.NewGuid();
        var domainUserId = Guid.NewGuid();
        var resolver = new MappingNotificationUserIdResolver(new Dictionary<Guid, Guid> { [identityUserId] = domainUserId });
        using var testHost = await StartHostAsync(resolver);
        using var client = testHost.CreateClient();
        var dto = new PushSubscriptionDto("https://push.example.test/resolved", "p256dh", "auth", null);

        var response = await SendAsync(client, HttpMethod.Post, "/api/notifications/push/subscription", identityUserId, JsonContent.Create(dto));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(await testHost.GetSubscriptionsForUserAsync(domainUserId));
        Assert.Empty(await testHost.GetSubscriptionsForUserAsync(identityUserId));
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesTheSubscription_UnderTheResolverSUserId_NotTheIdentityId()
    {
        var identityUserId = Guid.NewGuid();
        var domainUserId = Guid.NewGuid();
        var resolver = new MappingNotificationUserIdResolver(new Dictionary<Guid, Guid> { [identityUserId] = domainUserId });
        using var testHost = await StartHostAsync(resolver);
        using var client = testHost.CreateClient();
        await SendAsync(client, HttpMethod.Post, "/api/notifications/push/subscription", identityUserId,
            JsonContent.Create(new PushSubscriptionDto("https://push.example.test/resolved-remove", "p256dh", "auth", null)));

        var response = await SendAsync(client, HttpMethod.Delete, "/api/notifications/push/subscription?endpoint=https://push.example.test/resolved-remove", identityUserId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await testHost.GetSubscriptionsForUserAsync(domainUserId));
    }

    [Fact]
    public async Task InboxEndpoints_UseTheResolverSUserId_NotTheIdentityId()
    {
        var identityUserId = Guid.NewGuid();
        var domainUserId = Guid.NewGuid();
        var resolver = new MappingNotificationUserIdResolver(new Dictionary<Guid, Guid> { [identityUserId] = domainUserId });
        using var testHost = await StartHostAsync(resolver);
        using var client = testHost.CreateClient();
        testHost.InboxStore.UnreadByUser[domainUserId] = [new InboxNotificationDto(1, "Title", "Message", "Info", DateTime.UtcNow, false)];

        var unreadResponse = await SendAsync(client, HttpMethod.Get, "/api/notifications/unread", identityUserId);
        var markReadResponse = await SendAsync(client, HttpMethod.Post, "/api/notifications/1/read", identityUserId);

        Assert.Equal(HttpStatusCode.OK, unreadResponse.StatusCode);
        Assert.Single(await unreadResponse.Content.ReadFromJsonAsync<List<InboxNotificationDto>>() ?? []);
        Assert.Equal(HttpStatusCode.NoContent, markReadResponse.StatusCode);
        Assert.Equal([domainUserId], testHost.InboxStore.GetUnreadCalls);
        Assert.Equal([(domainUserId, 1L)], testHost.InboxStore.MarkReadCalls);
        Assert.DoesNotContain(identityUserId, testHost.InboxStore.GetUnreadCalls);
    }
}
