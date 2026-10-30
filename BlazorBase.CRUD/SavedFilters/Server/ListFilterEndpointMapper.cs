using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BlazorBase.CRUD.SavedFilters.Server;

/// <summary>
/// Maps the saved-filter endpoints. Every endpoint requires an authenticated user and acts only on that user's rows;
/// the user comes from the authenticated principal, never from the request.
/// </summary>
public static class ListFilterEndpointMapper
{
    /// <summary>
    /// Maps <c>GET {route}?listKey=</c>, <c>POST {route}?listKey=</c>, <c>PUT {route}/default?listKey=</c> and
    /// <c>DELETE {route}/{id}?listKey=</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="route">The route prefix.</param>
    public static IEndpointRouteBuilder MapBlazorBaseListFilters(this IEndpointRouteBuilder endpoints, string route = "api/list-filters")
    {
        var group = endpoints.MapGroup(route).RequireAuthorization();

        group.MapGet(String.Empty, GetAsync);
        group.MapPost(String.Empty, SaveAsync);
        group.MapPut("default", SetDefaultAsync);
        group.MapDelete("{id:guid}", DeleteAsync);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(string? listKey, IListFilterRepository repository, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        await RunAsync(user, async userId => Results.Ok(await repository.GetAsync(userId, listKey!, cancellationToken)));

    private static async Task<IResult> SaveAsync(string? listKey, SavedListFilterInput input, IListFilterRepository repository, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        await RunAsync(user, async userId => Results.Ok(await repository.SaveAsync(userId, listKey!, input, cancellationToken)));

    private static async Task<IResult> SetDefaultAsync(string? listKey, SetListFilterDefaultRequest request, IListFilterRepository repository, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        await RunAsync(user, async userId =>
            await repository.SetDefaultAsync(userId, listKey!, request.Id, cancellationToken) ? Results.NoContent() : Results.NotFound());

    private static async Task<IResult> DeleteAsync(Guid id, string? listKey, IListFilterRepository repository, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        await RunAsync(user, async userId =>
            await repository.DeleteAsync(userId, listKey!, id, cancellationToken) ? Results.NoContent() : Results.NotFound());

    private static async Task<IResult> RunAsync(ClaimsPrincipal user, Func<string, Task<IResult>> action)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        if (String.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            return await action(userId);
        }
        catch (ListFilterValidationException exception)
        {
            return Results.BadRequest(exception.Message);
        }
        catch (ListFilterNameConflictException exception)
        {
            return Results.Conflict(exception.Message);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
