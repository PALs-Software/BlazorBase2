using BlazorBase.User.Models;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorBase.User.Server.Controllers;

[ApiController]
[Route("api/auth")]
public abstract class AuthControllerBase<TUser>(
    UserManager<TUser> userManager,
    RoleManager<IdentityRole> roleManager,
    TokenService<TUser> tokenService,
    BaseUserDbContext<TUser> dbContext,
    IConfiguration configuration) : ControllerBase
    where TUser : BaseUser, new()
{
    #region Injects
    private readonly UserManager<TUser> UserManager = userManager;
    private readonly RoleManager<IdentityRole> RoleManager = roleManager;
    private readonly TokenService<TUser> TokenService = tokenService;
    private readonly BaseUserDbContext<TUser> DbContext = dbContext;
    private readonly IConfiguration Configuration = configuration;
    #endregion

    /// <summary>
    /// The role names to ensure exist and offer during setup.
    /// Override to supply a custom set; the default is <c>["Admin", "User"]</c>.
    /// </summary>
    protected virtual IReadOnlyList<string> RolesToSeed => ["Admin", "User"];

    /// <summary>
    /// Returns whether the system has any registered users (for first-user setup flow).
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthStatusResponse>> GetStatus()
    {
        var hasUsers = await UserManager.Users.AnyAsync();

        return Ok(new AuthStatusResponse
        {
            HasUsers = hasUsers,
            DevelopmentAuthenticationEnabled = ResolveDevelopmentSession()?.IsEnabled ?? false
        });
    }

    /// <summary>
    /// Creates the first admin user. Only works when no users exist.
    /// </summary>
    [HttpPost("setup")]
    [AllowAnonymous]
    [EnableRateLimiting("BlazorBaseAuth")]
    public async Task<ActionResult<LoginResponse>> Setup([FromBody] SetupRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (await UserManager.Users.AnyAsync())
            return Conflict(new { message = "System is already set up." });

        var user = new TUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
        };

        var result = await UserManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await EnsureRolesExist();
        await UserManager.AddToRoleAsync(user, "Admin");

        await OnAfterUserCreatedAsync(user, request);

        return Ok(await GenerateTokenResponse(user));
    }

    /// <summary>
    /// Authenticates a user and returns JWT tokens. Honors the lockout policy configured on
    /// <see cref="Microsoft.AspNetCore.Identity.IdentityOptions.Lockout"/>: failed attempts increment
    /// the user's lockout counter, locked-out accounts are rejected before the password is checked,
    /// and a successful login resets the counter.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("BlazorBaseAuth")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var user = await UserManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            NormalizeLoginTimingWithDummyHash(request.Password);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (await UserManager.IsLockedOutAsync(user))
        {
            NormalizeLoginTimingWithDummyHash(request.Password);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!await UserManager.CheckPasswordAsync(user, request.Password))
        {
            await UserManager.AccessFailedAsync(user);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!user.IsActive)
            return Unauthorized(new { message = "Account is deactivated." });

        await UserManager.ResetAccessFailedCountAsync(user);
        return Ok(await GenerateTokenResponse(user));
    }

    /// <summary>
    /// Performs a throwaway password hash so that rejected login attempts for a non-existent or
    /// locked-out account take comparable time to a real password check, denying an attacker a
    /// response-timing account-enumeration oracle.
    /// </summary>
    private void NormalizeLoginTimingWithDummyHash(string password) =>
        UserManager.PasswordHasher.HashPassword(new TUser(), password);

    /// <summary>
    /// Issues a new access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("BlazorBaseAuth")]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        var presentedToken = await ResolveRefreshTokenAsync(request);

        if (presentedToken is null)
            return ValidationProblem(ModelState);

        var tokenHash = TokenService.HashRefreshToken(presentedToken);

        var storedToken = await DbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == tokenHash);

        if (storedToken is null)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        if (storedToken.IsRevoked)
        {
            if (storedToken.FamilyId != Guid.Empty)
            {
                await DbContext.RefreshTokens
                    .Where(r => r.FamilyId == storedToken.FamilyId && r.UserId == storedToken.UserId && !r.IsRevoked)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IsRevoked, true));

                var logger = HttpContext?.RequestServices?.GetService<ILogger<AuthControllerBase<TUser>>>();
                logger?.LogWarning("Refresh-token reuse detected for user {UserId}; revoking token family {FamilyId}.", storedToken.UserId, storedToken.FamilyId);
            }

            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        var user = await UserManager.FindByIdAsync(storedToken.UserId);
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Account is deactivated." });

        if (await UserManager.IsLockedOutAsync(user))
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        var strategy = DbContext.Database.CreateExecutionStrategy();
        var response = await strategy.ExecuteAsync<LoginResponse?>(async () =>
        {
            await using var transaction = await DbContext.Database.BeginTransactionAsync();

            var revoked = await DbContext.RefreshTokens
                .Where(r => r.Id == storedToken.Id && !r.IsRevoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IsRevoked, true));

            if (revoked == 0)
                return null;

            var tokenResponse = await GenerateTokenResponse(user, storedToken.FamilyId);
            await transaction.CommitAsync();
            return tokenResponse;
        });

        if (response is null)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        return Ok(response);
    }

    /// <summary>
    /// Revokes the provided refresh token (logout). Anonymous so it also succeeds when the
    /// caller's access token has already expired — possession of the refresh token is the
    /// authority to revoke it, and only that single token is revoked.
    /// </summary>
    /// <remarks>
    /// Anonymous, like <see cref="Refresh"/>: the presented refresh token is itself the
    /// credential, and this endpoint is strictly weaker than <c>refresh</c>, which anyone
    /// holding that token could call instead to obtain a full access token. Requiring a
    /// bearer here would make the endpoint unreachable for clients, because
    /// <c>AuthTokenHandler</c> depends on <c>IAuthService</c> and therefore cannot be
    /// attached to the auth client without closing a dependency cycle. Unknown tokens get
    /// the same 204 as known ones, so the response is no existence oracle.
    /// </remarks>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        var presentedToken = await ResolveRefreshTokenAsync(request);

        if (HttpContext is not null)
            await ResolveTokenTransport().ClearAsync(HttpContext);

        // Nothing presented is the same outcome as an unknown token: logout is idempotent, and
        // answering differently would turn the endpoint into an existence oracle.
        if (presentedToken is null)
            return NoContent();

        var tokenHash = TokenService.HashRefreshToken(presentedToken);

        var token = await DbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == tokenHash && !r.IsRevoked);

        if (token is null)
            return NoContent();

        token.IsRevoked = true;
        await DbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Signs the caller in as the configured development account and returns normal tokens.
    /// Answers <c>404</c> unless development authentication is both switched on and running in
    /// the Development environment, so in every other environment the route does not exist.
    /// </summary>
    /// <remarks>
    /// This issues a real token for a real account rather than faking a principal, so the
    /// authorization pipeline a developer exercises is the one that ships. The account and its
    /// roles come from the <c>DevelopmentAuthentication</c> configuration section.
    /// </remarks>
    [HttpPost("development-login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> DevelopmentLogin(CancellationToken cancellationToken)
    {
        var developmentSession = ResolveDevelopmentSession();

        if (developmentSession is null || !developmentSession.IsEnabled)
            return NotFound();

        var user = await developmentSession.ResolveUserAsync(cancellationToken);

        if (user is null)
            return Problem(detail: "The development account could not be provisioned. See the server log.", statusCode: StatusCodes.Status500InternalServerError);

        return Ok(await GenerateTokenResponse(user));
    }

    /// <summary>
    /// Resolved from the request rather than the constructor so hosts that already derive from
    /// this controller do not have to change their constructor to pick up the feature.
    /// </summary>
    private IDevelopmentSessionService<TUser>? ResolveDevelopmentSession()
        => HttpContext.RequestServices?.GetService<IDevelopmentSessionService<TUser>>();

    /// <summary>
    /// Hook called after the setup user has been created and assigned the Admin role,
    /// before token generation. Override to perform host-specific initialization such as
    /// creating a first household or seeding data.
    /// </summary>
    /// <param name="user">The newly created admin user.</param>
    /// <param name="request">The original setup request, including optional fields such as <see cref="SetupRequest.HouseholdName"/>.</param>
    protected virtual Task OnAfterUserCreatedAsync(TUser user, SetupRequest request) => Task.CompletedTask;

    protected async Task<LoginResponse> GenerateTokenResponse(TUser user, Guid? familyId = null)
    {
        var roles = await UserManager.GetRolesAsync(user);
        var tokens = await TokenService.IssueTokenPairAsync(user, roles, DbContext, familyId);

        if (HttpContext is null)
            return tokens;

        return await ResolveTokenTransport().WriteAsync(HttpContext, tokens);
    }

    /// <summary>
    /// Resolved per request rather than through the constructor, so a host that already derives from
    /// this controller picks the feature up without changing its own constructor signature. Falls back
    /// to the body transport when nothing is registered, which keeps a host that predates this seam —
    /// or a test host that composes services by hand — working exactly as before.
    /// </summary>
    private IAuthTokenTransport ResolveTokenTransport()
        => HttpContext?.RequestServices?.GetService<IAuthTokenTransport>() ?? new BodyAuthTokenTransport();

    /// <summary>
    /// The refresh token as the active transport sees it. A cookie-based transport ignores the body
    /// entirely, so the request model's <c>[Required]</c> cannot be validated before this runs.
    /// </summary>
    private async Task<string?> ResolveRefreshTokenAsync(RefreshTokenRequest? request)
    {
        var fromBody = string.IsNullOrEmpty(request?.RefreshToken) ? null : request.RefreshToken;

        if (HttpContext is null)
            return fromBody;

        var resolved = await ResolveTokenTransport().ReadRefreshTokenAsync(HttpContext, fromBody);

        return string.IsNullOrEmpty(resolved) ? null : resolved;
    }

    private async Task EnsureRolesExist()
    {
        foreach (var roleName in RolesToSeed)
        {
            if (!await RoleManager.RoleExistsAsync(roleName))
                await RoleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}
