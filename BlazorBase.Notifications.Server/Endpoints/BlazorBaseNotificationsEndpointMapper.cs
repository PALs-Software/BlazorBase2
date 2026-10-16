using System.Security.Claims;
using BlazorBase.Notifications.Models;
using BlazorBase.Notifications.Server.Configuration;
using BlazorBase.Notifications.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace BlazorBase.Notifications.Server.Endpoints;

/// <summary>
/// Maps the REST surface for in-app notifications and Web Push subscriptions. Every endpoint
/// requires an authenticated caller; the acting user id always comes from the
/// <see cref="ClaimTypes.NameIdentifier"/> claim on the request, never from the request body, so a
/// caller can only ever act on their own notifications and subscriptions.
/// </summary>
public static class BlazorBaseNotificationsEndpointMapper
{
    /// <summary>Maps every notification and push-subscription endpoint under <paramref name="path"/>.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="path">Route prefix. Defaults to <c>api/notifications</c>.</param>
    public static IEndpointRouteBuilder MapBlazorBaseNotifications(this IEndpointRouteBuilder endpoints, string path = "api/notifications")
    {
        var group = endpoints.MapGroup(path).RequireAuthorization();

        group.MapGet("push/public-key", GetPublicKey);
        group.MapPost("push/subscription", SubscribeAsync);
        group.MapDelete("push/subscription", UnsubscribeAsync);
        group.MapPut("push/subscription/enabled", SetSubscriptionEnabledAsync);

        group.MapGet("unread", GetUnreadAsync);
        group.MapGet("unread/count", GetUnreadCountAsync);
        group.MapPost("{id:long}/read", MarkReadAsync);
        group.MapPost("{id:long}/dismiss", DismissAsync);

        return endpoints;
    }

    private static IResult GetPublicKey(IOptions<WebPushOptions> options) =>
        Results.Ok(new PushPublicKeyResponse(options.Value.PublicKey ?? string.Empty));

    private static async Task<IResult> SubscribeAsync(PushSubscriptionDto request, HttpContext httpContext, IPushSubscriptionStore store, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(httpContext.User, out var userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Endpoint) || string.IsNullOrWhiteSpace(request.P256dh) || string.IsNullOrWhiteSpace(request.Auth))
            return Results.BadRequest();

        await store.UpsertAsync(userId, request.Endpoint, request.P256dh, request.Auth, request.UserAgent, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UnsubscribeAsync(string endpoint, HttpContext httpContext, IPushSubscriptionStore store, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(httpContext.User, out var userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(endpoint))
            return Results.BadRequest();

        var removed = await store.RemoveByEndpointAsync(userId, endpoint, cancellationToken);
        return removed ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> SetSubscriptionEnabledAsync(SetPushSubscriptionEnabledRequest request, HttpContext httpContext, IPushSubscriptionStore store, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(httpContext.User, out var userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Endpoint))
            return Results.BadRequest();

        var updated = await store.SetEnabledAsync(userId, request.Endpoint, request.Enabled, cancellationToken);
        return updated ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> GetUnreadAsync(HttpContext httpContext, INotificationInboxStore inboxStore, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(httpContext.User, out var userId))
            return Results.Unauthorized();

        var unread = await inboxStore.GetUnreadAsync(userId, cancellationToken);
        return Results.Ok(unread);
    }

    private static async Task<IResult> GetUnreadCountAsync(HttpContext httpContext, INotificationInboxStore inboxStore, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(httpContext.User, out var userId))
            return Results.Unauthorized();

        var count = await inboxStore.GetUnreadCountAsync(userId, cancellationToken);
        return Results.Ok(count);
    }

    private static async Task<IResult> MarkReadAsync(long id, HttpContext httpContext, INotificationInboxStore inboxStore, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(httpContext.User, out var userId))
            return Results.Unauthorized();

        await inboxStore.MarkReadAsync(userId, id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DismissAsync(long id, HttpContext httpContext, INotificationInboxStore inboxStore, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(httpContext.User, out var userId))
            return Results.Unauthorized();

        await inboxStore.DismissAsync(userId, id, cancellationToken);
        return Results.NoContent();
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
