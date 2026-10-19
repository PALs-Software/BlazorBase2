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

namespace BlazorBase.User.Server.Controllers;

[ApiController]
[Route("api/auth")]
public abstract class AuthControllerBase<TUser>(
    UserManager<TUser> userManager,
    SignInManager<TUser> signInManager,
    RoleManager<IdentityRole> roleManager,
    AuthTokenIssuer<TUser> tokenIssuer,
    TwoFactorPendingTokenService<TUser> pendingTokenService) : ControllerBase
    where TUser : BaseUser, new()
{
    #region Injects
    private readonly UserManager<TUser> UserManager = userManager;
    private readonly SignInManager<TUser> SignInManager = signInManager;
    private readonly RoleManager<IdentityRole> RoleManager = roleManager;
    private readonly AuthTokenIssuer<TUser> TokenIssuer = tokenIssuer;
    private readonly TwoFactorPendingTokenService<TUser> PendingTokenService = pendingTokenService;
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
    /// <remarks>
    /// <c>UserManager.Users.AnyAsync()</c> below is a cheap early exit, not the race guard: two
    /// concurrent requests can both observe no users and both reach <see cref="UserManager{TUser}.CreateAsync(TUser, string)"/>.
    /// The guard is <see cref="SetupCompletionMarker"/>'s primary key - inserted in the same
    /// transaction as the new user, so a losing request fails on a constraint violation before it
    /// ever creates an account, and wins under any number of application instances because the
    /// database enforces it, not this process.
    /// </remarks>
    [HttpPost("setup")]
    [AllowAnonymous]
    [EnableRateLimiting("BlazorBaseAuth")]
    public async Task<ActionResult<LoginResponse>> Setup([FromBody] SetupRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (await UserManager.Users.AnyAsync())
            return Conflict(new { message = "System is already set up." });

        var dbContext = HttpContext.RequestServices.GetRequiredService<BaseUserDbContext<TUser>>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        dbContext.SetupCompletionMarkers.Add(new SetupCompletionMarker());
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "System is already set up." });
        }

        var user = new TUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
        };

        var result = await UserManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        await EnsureRolesExist();
        await UserManager.AddToRoleAsync(user, "Admin");

        await OnAfterUserCreatedAsync(user, request);

        await transaction.CommitAsync();

        return Ok(await GenerateTokenResponse(user));
    }

    /// <summary>
    /// Authenticates a user and returns JWT tokens. Honors the lockout policy configured on
    /// <see cref="Microsoft.AspNetCore.Identity.IdentityOptions.Lockout"/>: failed attempts increment
    /// the user's lockout counter, locked-out accounts are rejected before the password is checked,
    /// and a successful login resets the counter. A two-factor-enabled account instead answers
    /// <see cref="StatusCodes.Status202Accepted"/> with a pending token; see <see cref="LoginTwoFactor"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="SignInManager{TUser}.CheckPasswordSignInAsync"/> rather than
    /// <c>PasswordSignInAsync</c>: the latter signs the caller into a scheme and writes a cookie,
    /// which would change this endpoint's contract from "returns a JWT" to "returns a JWT and sets a
    /// cookie". <c>CheckPasswordSignInAsync</c> runs the same password check and lockout bookkeeping
    /// without touching the response; the two-factor requirement is detected separately below through
    /// public <c>UserManager</c> calls, so nothing here ever writes
    /// <see cref="Microsoft.AspNetCore.Identity.IdentityConstants.TwoFactorUserIdScheme"/>'s cookie.
    ///
    /// <c>IsActive</c> is this framework's own flag and unknown to Identity, so it is checked
    /// separately - but folded into the same generic answer, because a distinguishable response
    /// would tell an unauthenticated caller that the account exists.
    /// </remarks>
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

        var signInResult = await SignInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        // Lockout and not-allowed short-circuit before any password hash, so normalise them too.
        if (signInResult.IsLockedOut || signInResult.IsNotAllowed)
            NormalizeLoginTimingWithDummyHash(request.Password);

        if (!signInResult.Succeeded || !user.IsActive)
            return Unauthorized(new { message = "Invalid email or password." });

        if (await RequiresTwoFactorAsync(user))
            return Accepted(new TwoFactorRequiredResponse { PendingToken = PendingTokenService.Protect(user) });

        return Ok(await GenerateTokenResponse(user));
    }

    /// <summary>
    /// Completes a login that <see cref="Login"/> answered with <see cref="StatusCodes.Status202Accepted"/>
    /// because the account requires a second factor. Re-checks <see cref="BaseUser.IsActive"/> and
    /// lockout exactly like <see cref="Login"/>: the pending token only proves the password was correct
    /// a few minutes ago, not that the account is still eligible to sign in now.
    /// </summary>
    /// <remarks>
    /// A wrong code counts toward the same lockout as a wrong password, through
    /// <see cref="UserManager{TUser}.AccessFailedAsync"/>, so the pending token cannot be used to
    /// brute-force the code without limit.
    /// <para>
    /// Consuming the token is the last step and its result decides the outcome: two requests racing
    /// with the same pending token both pass verification, but only the one that wins the atomic
    /// consume gets a session. Consuming after verification rather than before is what keeps a wrong
    /// code from burning the token.
    /// </para>
    /// </remarks>
    [HttpPost("login/2fa")]
    [AllowAnonymous]
    [EnableRateLimiting("BlazorBaseAuth")]
    public async Task<ActionResult<LoginResponse>> LoginTwoFactor([FromBody] TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = await PendingTokenService.UnprotectAsync(request.PendingToken, cancellationToken);
        if (userId is null)
            return Unauthorized(new { message = "Invalid or expired two-factor session." });

        var user = await UserManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive || await UserManager.IsLockedOutAsync(user))
            return Unauthorized(new { message = "Invalid or expired two-factor session." });

        var verified = request.IsRecoveryCode
            ? (await UserManager.RedeemTwoFactorRecoveryCodeAsync(user, request.Code)).Succeeded
            : await UserManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.Code);

        if (!verified)
        {
            await UserManager.AccessFailedAsync(user);
            return Unauthorized(new { message = "Invalid code." });
        }

        if (!await PendingTokenService.ConsumeAsync(request.PendingToken, cancellationToken))
            return Unauthorized(new { message = "Invalid or expired two-factor session." });

        return Ok(await GenerateTokenResponse(user));
    }

    /// <summary>
    /// Mirrors the check <c>SignInManager.SignInOrTwoFactorAsync</c> makes internally, without its side
    /// effect of writing <see cref="Microsoft.AspNetCore.Identity.IdentityConstants.TwoFactorUserIdScheme"/>'s
    /// cookie - see the class remarks on <see cref="Login"/> for why that cookie is avoided here.
    /// </summary>
    private async Task<bool> RequiresTwoFactorAsync(TUser user)
    {
        if (!UserManager.SupportsUserTwoFactor || !await UserManager.GetTwoFactorEnabledAsync(user))
            return false;

        return (await UserManager.GetValidTwoFactorProvidersAsync(user)).Count > 0;
    }

    /// <summary>
    /// Performs a throwaway password hash so that rejected login attempts for a non-existent or
    /// locked-out account take comparable time to a real password check, denying an attacker a
    /// response-timing account-enumeration oracle.
    /// </summary>
    private void NormalizeLoginTimingWithDummyHash(string password) =>
        UserManager.PasswordHasher.HashPassword(new TUser(), password);

    /// <summary>
    /// Issues a new access/refresh token pair using a valid refresh token.
    /// </summary>
    /// <remarks>
    /// The refresh token is an opaque value; only its hash lives in the database, alongside a
    /// rotation family and the security stamp captured when it was minted. A presented token that is
    /// unknown, already revoked, or expired is rejected outright - a revoked token being presented
    /// again is a replay, so the entire family is revoked and the attempt logged, the reuse-detection
    /// signal the rotation scheme exists to surface. A token that passes those checks is re-validated
    /// against the live account (active, not locked out, security stamp unchanged since issuance)
    /// before it is atomically revoked and replaced in the same family. Unlike the token itself, the
    /// user's security stamp is never touched here - rotating a token must not sign the user out of
    /// every other session, only replace the one token that was presented.
    /// </remarks>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("BlazorBaseAuth")]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (HttpContext is not null && !await ResolveTokenTransport().IsRequestIntentionalAsync(HttpContext))
            return StatusCode(StatusCodes.Status403Forbidden);

        var presentedToken = await ResolveRefreshTokenAsync(request);

        if (presentedToken is null)
            return ValidationProblem(ModelState);

        var stored = await TokenIssuer.FindAsync(presentedToken);
        if (stored is null)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        if (stored.IsRevoked)
        {
            await TokenIssuer.RevokeFamilyAfterReuseDetectedAsync(stored.UserId, stored.FamilyId);
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        if (TokenIssuer.IsExpired(stored))
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        var user = await TokenIssuer.ValidateSecurityStampAsync(stored);
        if (user is null)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        if (!user.IsActive)
            return Unauthorized(new { message = "Account is deactivated." });

        if (await UserManager.IsLockedOutAsync(user))
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        var principal = await SignInManager.CreateUserPrincipalAsync(user);
        var tokens = await TokenIssuer.RotateAsync(stored, principal);
        if (tokens is null)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        if (HttpContext is null)
            return Ok(tokens);

        return Ok(await ResolveTokenTransport().WriteAsync(HttpContext, tokens, principal));
    }

    /// <summary>
    /// Revokes the presented refresh token's family (that device's session lineage) and clears the
    /// transport. Anonymous so it also succeeds when the caller's access token has already expired —
    /// possession of the refresh token is the authority to log out with. Never touches the user's
    /// security stamp: logging out of one device must not sign the user out of every other one.
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
    [EnableRateLimiting("BlazorBaseAuth")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        if (HttpContext is not null && !await ResolveTokenTransport().IsRequestIntentionalAsync(HttpContext))
            return StatusCode(StatusCodes.Status403Forbidden);

        var presentedToken = await ResolveRefreshTokenAsync(request);

        if (HttpContext is not null)
            await ResolveTokenTransport().ClearAsync(HttpContext);

        // Nothing presented is the same outcome as an unknown token: logout is idempotent, and
        // answering differently would turn the endpoint into an existence oracle.
        if (presentedToken is null)
            return NoContent();

        var stored = await TokenIssuer.FindAsync(presentedToken);
        if (stored is null)
            return NoContent();

        await TokenIssuer.RevokeFamilyAsync(stored.UserId, stored.FamilyId);

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
    [EnableRateLimiting("BlazorBaseAuth")]
    public async Task<ActionResult<LoginResponse>> DevelopmentLogin(CancellationToken cancellationToken)
    {
        var developmentSession = ResolveDevelopmentSession();

        if (developmentSession is null || !developmentSession.IsEnabled)
            return NotFound();

        var user = await developmentSession.ResolveUserAsync(cancellationToken);

        if (user is null)
            return Problem(detail: "The development account could not be provisioned. See the server log.", statusCode: StatusCodes.Status500InternalServerError);

        await OnAfterDevelopmentLoginAsync(user);

        return Ok(await GenerateTokenResponse(user));
    }

    /// <summary>
    /// Resolved from the request rather than the constructor so hosts that already derive from
    /// this controller do not have to change their constructor to pick up the feature.
    /// </summary>
    private IDevelopmentSessionService<TUser>? ResolveDevelopmentSession()
        => HttpContext.RequestServices?.GetService<IDevelopmentSessionService<TUser>>();

    /// <summary>
    /// Hook called every time the development account is resolved (created or reused), before token
    /// generation. Override to provision or link host-specific data by email — such as a domain user
    /// row that is otherwise only ever created through the real sign-up or external sign-in paths —
    /// so a fresh development account behaves like one as soon as it signs in once.
    /// </summary>
    /// <param name="user">The resolved development account. Already exists in <see cref="UserManager{TUser}"/>.</param>
    protected virtual Task OnAfterDevelopmentLoginAsync(TUser user) => Task.CompletedTask;

    /// <summary>
    /// Hook called after the setup user has been created and assigned the Admin role,
    /// before token generation. Override to perform host-specific initialization such as
    /// creating a first household or seeding data.
    /// </summary>
    /// <param name="user">The newly created admin user.</param>
    /// <param name="request">The original setup request, including optional fields such as <see cref="SetupRequest.HouseholdName"/>.</param>
    protected virtual Task OnAfterUserCreatedAsync(TUser user, SetupRequest request) => Task.CompletedTask;

    protected async Task<LoginResponse> GenerateTokenResponse(TUser user)
    {
        var principal = await SignInManager.CreateUserPrincipalAsync(user);
        var tokens = await TokenIssuer.IssueTokenPairAsync(principal);

        if (HttpContext is null)
            return tokens;

        return await ResolveTokenTransport().WriteAsync(HttpContext, tokens, principal);
    }

    /// <summary>
    /// Resolved per request rather than through the constructor, so a host that already derives from
    /// this controller picks the feature up without changing its own constructor signature. Falls back
    /// to the bearer transport when nothing is registered, which keeps a host that predates this seam —
    /// or a test host that composes services by hand — working exactly as before.
    /// </summary>
    private IAuthTokenTransport ResolveTokenTransport()
        => HttpContext?.RequestServices?.GetService<IAuthTokenTransport>() ?? new IdentityBearerAuthTransport();

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
