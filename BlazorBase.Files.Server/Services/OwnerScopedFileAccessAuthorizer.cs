using System.Security.Claims;
using BlazorBase.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Secure-by-default <see cref="IFileAccessAuthorizer"/> that enforces per-user ownership: a
/// file's <see cref="BaseFile.OwnerScopeKey"/> must equal the acting principal's
/// <see cref="ClaimTypes.NameIdentifier"/> claim. This is the default registered by
/// <see cref="BlazorBaseFilesServerServiceCollectionExtensions.AddBlazorBaseFilesServer{TContext}"/>.
/// </summary>
public class OwnerScopedFileAccessAuthorizer(DbContext dbContext) : IFileAccessAuthorizer
{
    #region Injects
    private readonly DbContext DbContext = dbContext;
    #endregion

    /// <inheritdoc/>
    public async Task<bool> CanAccessAsync(Guid fileId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return false;

        var file = await DbContext.Set<BaseFile>().FindAsync([fileId], cancellationToken).ConfigureAwait(false);
        if (file is null)
            return false;

        return string.Equals(file.OwnerScopeKey, userId, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public Task<bool> CanAssignScopeAsync(string? ownerScopeKey, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Task.FromResult(false);

        return Task.FromResult(ownerScopeKey is null || string.Equals(ownerScopeKey, userId, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public string? ResolveDefaultOwnerScope(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);
}
