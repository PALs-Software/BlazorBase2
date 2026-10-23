using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BlazorBase.Dashboard.Server;

/// <summary>
/// Maps the dashboard-layout endpoints. Every endpoint requires an authenticated user and acts only on that user's rows; the
/// user comes from the authenticated principal, never from the request.
/// </summary>
public static class DashboardLayoutEndpointMapper
{
    /// <summary>
    /// Maps <c>GET {route}?dashboardKey=</c>, <c>PUT {route}?dashboardKey=</c> and <c>DELETE {route}?dashboardKey=</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="route">The route prefix.</param>
    public static IEndpointRouteBuilder MapBlazorBaseDashboardLayouts(this IEndpointRouteBuilder endpoints, string route = "api/dashboard-layouts")
    {
        var group = endpoints.MapGroup(route).RequireAuthorization();

        group.MapGet(String.Empty, GetAsync);
        group.MapPut(String.Empty, SaveAsync);
        group.MapDelete(String.Empty, DeleteAsync);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(string? dashboardKey, IDashboardLayoutRepository repository, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        await RunAsync(user, async userId => Results.Ok(await repository.GetAsync(userId, dashboardKey!, cancellationToken)));

    private static async Task<IResult> SaveAsync(string? dashboardKey, DashboardLayout layout, IDashboardLayoutRepository repository, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        await RunAsync(user, async userId =>
        {
            await repository.SaveAsync(userId, dashboardKey!, layout, cancellationToken);
            return Results.NoContent();
        });

    private static async Task<IResult> DeleteAsync(string? dashboardKey, IDashboardLayoutRepository repository, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        await RunAsync(user, async userId =>
        {
            await repository.DeleteAsync(userId, dashboardKey!, cancellationToken);
            return Results.NoContent();
        });

    private static async Task<IResult> RunAsync(ClaimsPrincipal user, Func<string, Task<IResult>> action)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        if (String.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            return await action(userId);
        }
        catch (DashboardLayoutValidationException exception)
        {
            return Results.BadRequest(exception.Message);
        }
    }
}
