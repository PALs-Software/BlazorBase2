using System.Security.Cryptography;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.Services;

public class DevelopmentSessionService<TUser>(
    UserManager<TUser> userManager,
    RoleManager<IdentityRole> roleManager,
    BaseUserDbContext<TUser> dbContext,
    IOptions<DevelopmentAuthenticationOptions> options,
    IHostEnvironment environment,
    ILogger<DevelopmentSessionService<TUser>> logger) : IDevelopmentSessionService<TUser>
    where TUser : BaseUser, new()
{
    #region Injects
    private readonly UserManager<TUser> UserManager = userManager;
    private readonly RoleManager<IdentityRole> RoleManager = roleManager;
    private readonly BaseUserDbContext<TUser> DbContext = dbContext;
    private readonly DevelopmentAuthenticationOptions Options = options.Value;
    private readonly IHostEnvironment Environment = environment;
    private readonly ILogger<DevelopmentSessionService<TUser>> Logger = logger;
    #endregion

    /// <summary>
    /// The environment is re-checked here and not only at registration, so a configuration
    /// reload cannot open this path in a non-development environment.
    /// </summary>
    public bool IsEnabled => Options.Enabled && Environment.IsDevelopment();

    public async Task<TUser?> ResolveUserAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return null;

        var user = await UserManager.FindByEmailAsync(Options.Email);

        if (user is null)
        {
            user = await CreateUserAsync();

            if (user is null)
                return null;
        }

        await SynchronizeRolesAsync(user);
        await DiscardPreviousRefreshTokensAsync(user, cancellationToken);

        Logger.LogWarning(
            "Development authentication signed in {Email} with roles {Roles}. This path exists only in the Development environment.",
            Options.Email,
            string.Join(", ", Options.EffectiveRoles));

        return user;
    }

    /// <summary>
    /// Drops the development account's earlier refresh tokens, because a token is issued on every
    /// app start — every reload, every server restart — and nothing would ever clean them up.
    /// Without this the table collects a row per reload and grows into the hundreds over a day of
    /// development.
    /// </summary>
    /// <remarks>
    /// Deleted rather than revoked: a throw-away development account has no session history worth
    /// auditing. The trade-off is that a second browser session against the same server loses its
    /// refresh token when the first one starts — in which case it signs in again on its next
    /// reload, which is exactly what this feature does anyway.
    /// </remarks>
    private async Task DiscardPreviousRefreshTokensAsync(TUser user, CancellationToken cancellationToken)
        => await DbContext.RefreshTokens
            .Where(token => token.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);

    private async Task<TUser?> CreateUserAsync()
    {
        var user = new TUser
        {
            UserName = Options.Email,
            Email = Options.Email,
            DisplayName = Options.DisplayName,
            EmailConfirmed = true
        };

        // The account is reached through this endpoint, never through the password form, so it
        // gets a throw-away secret nobody holds rather than a guessable default.
        var result = await UserManager.CreateAsync(user, CreateUnguessablePassword());

        if (result.Succeeded)
            return user;

        Logger.LogError(
            "Development authentication could not create {Email}: {Errors}",
            Options.Email,
            string.Join(" ", result.Errors.Select(error => error.Description)));

        return null;
    }

    /// <summary>
    /// Adds the configured roles and removes the ones no longer listed, so narrowing the
    /// configuration actually narrows what the developer can reach.
    /// </summary>
    private async Task SynchronizeRolesAsync(TUser user)
    {
        var configuredRoles = Options.EffectiveRoles;

        foreach (var role in configuredRoles)
        {
            if (!await RoleManager.RoleExistsAsync(role))
                await RoleManager.CreateAsync(new IdentityRole(role));
        }

        var currentRoles = await UserManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(configuredRoles).ToList();
        var rolesToAdd = configuredRoles.Except(currentRoles).ToList();

        if (rolesToRemove.Count > 0)
            await UserManager.RemoveFromRolesAsync(user, rolesToRemove);

        if (rolesToAdd.Count > 0)
            await UserManager.AddToRolesAsync(user, rolesToAdd);
    }

    private static string CreateUnguessablePassword()
        => $"Dev-{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}1!";
}
