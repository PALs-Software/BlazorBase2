using BlazorBase.User.Models;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Server.Invitations.Endpoints;

/// <summary>Maps the invitation API of <see cref="UserInvitationService{TUser}"/>.</summary>
public static class UserInvitationEndpointMapper
{
    /// <summary>The rate-limiter policy registered by <c>AddBlazorBaseUserServer</c>.</summary>
    private const string RateLimitPolicy = "BlazorBaseAuth";

    private const string ErrorCodeExtension = "code";

    /// <summary>
    /// Maps the administration endpoints (list, create, resend, renew link, revoke - restricted to
    /// <paramref name="adminRoles"/>) and the two anonymous accept endpoints (preview and accept, both
    /// rate-limited by the "BlazorBaseAuth" policy) under <paramref name="path"/>. Failures answer as
    /// problem details carrying a <see cref="UserInvitationErrorCodes"/> value in <c>code</c>.
    /// </summary>
    /// <param name="adminRoles">Comma-separated roles that may manage invitations.</param>
    /// <exception cref="InvalidOperationException"><c>AddBlazorBaseUserInvitations</c> was not called.</exception>
    public static IEndpointRouteBuilder MapBlazorBaseUserInvitationEndpoints<TUser>(
        this IEndpointRouteBuilder endpoints,
        string path = "api/auth/invitations",
        string adminRoles = "Admin")
        where TUser : BaseUser, new()
    {
        EnsureRegistered<TUser>(endpoints);

        var group = endpoints.MapGroup(path);
        var administratorPolicy = new AuthorizeAttribute { Roles = adminRoles };

        group.MapGet(string.Empty, ListAsync<TUser>).RequireAuthorization(administratorPolicy);
        group.MapPost(string.Empty, CreateAsync<TUser>).RequireAuthorization(administratorPolicy);
        group.MapPost("{invitationId:guid}/resend", ResendAsync<TUser>).RequireAuthorization(administratorPolicy);
        group.MapPost("{invitationId:guid}/link", RenewLinkAsync<TUser>).RequireAuthorization(administratorPolicy);
        group.MapDelete("{invitationId:guid}", RevokeAsync<TUser>).RequireAuthorization(administratorPolicy);

        group.MapGet("accept", PreviewAsync<TUser>).AllowAnonymous().RequireRateLimiting(RateLimitPolicy);
        group.MapPost("accept", AcceptAsync<TUser>).AllowAnonymous().RequireRateLimiting(RateLimitPolicy);

        return endpoints;
    }

    private static void EnsureRegistered<TUser>(IEndpointRouteBuilder endpoints)
        where TUser : BaseUser, new()
    {
        var serviceProviderIsService = endpoints.ServiceProvider.GetService<IServiceProviderIsService>();

        if (serviceProviderIsService?.IsService(typeof(UserInvitationService<TUser>)) == true)
            return;

        throw new InvalidOperationException($"The invitation endpoints require services.AddBlazorBaseUserInvitations<{typeof(TUser).Name}>() to have been called.");
    }

    private static async Task<IResult> ListAsync<TUser>(HttpContext httpContext, UserInvitationService<TUser> invitationService)
        where TUser : BaseUser, new() =>
        Results.Ok(await invitationService.ListAsync(httpContext.User, httpContext.RequestAborted));

    private static async Task<IResult> CreateAsync<TUser>(HttpContext httpContext, CreateUserInvitationRequest request, UserInvitationService<TUser> invitationService)
        where TUser : BaseUser, new()
    {
        var result = await invitationService.CreateAsync(request, httpContext.User, httpContext.RequestAborted);

        return result.Succeeded
            ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
            : ToProblem(result);
    }

    private static async Task<IResult> ResendAsync<TUser>(HttpContext httpContext, Guid invitationId, UserInvitationService<TUser> invitationService)
        where TUser : BaseUser, new()
    {
        var result = await invitationService.ResendAsync(invitationId, httpContext.User, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static async Task<IResult> RenewLinkAsync<TUser>(HttpContext httpContext, Guid invitationId, UserInvitationService<TUser> invitationService)
        where TUser : BaseUser, new()
    {
        var result = await invitationService.RenewLinkAsync(invitationId, httpContext.User, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static async Task<IResult> RevokeAsync<TUser>(HttpContext httpContext, Guid invitationId, UserInvitationService<TUser> invitationService)
        where TUser : BaseUser, new()
    {
        var result = await invitationService.RevokeAsync(invitationId, httpContext.User, httpContext.RequestAborted);
        return result.Succeeded ? Results.NoContent() : ToProblem(result);
    }

    private static async Task<IResult> PreviewAsync<TUser>(HttpContext httpContext, string? token, UserInvitationService<TUser> invitationService)
        where TUser : BaseUser, new() =>
        Results.Ok(await invitationService.PreviewAsync(token, httpContext.RequestAborted));

    /// <summary>
    /// Signs the new account in exactly as login does - a token pair from <see cref="AuthTokenIssuer{TUser}"/>
    /// delivered through the active <see cref="IAuthTokenTransport"/> - so the accept page lands the person
    /// in the application without a second password prompt.
    /// </summary>
    private static async Task<IResult> AcceptAsync<TUser>(
        HttpContext httpContext,
        AcceptUserInvitationRequest request,
        UserInvitationService<TUser> invitationService,
        SignInManager<TUser> signInManager,
        AuthTokenIssuer<TUser> tokenIssuer)
        where TUser : BaseUser, new()
    {
        var result = await invitationService.AcceptAsync(request, httpContext.RequestAborted);
        if (!result.Succeeded)
            return ToProblem(result);

        var principal = await signInManager.CreateUserPrincipalAsync(result.Value!);
        var tokens = await tokenIssuer.IssueTokenPairAsync(principal, cancellationToken: httpContext.RequestAborted);
        var tokenTransport = httpContext.RequestServices.GetService<IAuthTokenTransport>() ?? new IdentityBearerAuthTransport();

        return Results.Ok(await tokenTransport.WriteAsync(httpContext, tokens, principal, httpContext.RequestAborted));
    }

    private static IResult ToProblem<TValue>(UserInvitationResult<TValue> result)
    {
        var extensions = new Dictionary<string, object?> { [ErrorCodeExtension] = result.ErrorCode };

        var statusCode = result.Outcome switch
        {
            UserInvitationOutcome.NotFound => StatusCodes.Status404NotFound,
            UserInvitationOutcome.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        if (result.Errors is not null)
            return Results.ValidationProblem(result.Errors, detail: result.ErrorMessage, statusCode: statusCode, extensions: extensions);

        return Results.Problem(detail: result.ErrorMessage, statusCode: statusCode, extensions: extensions);
    }
}
