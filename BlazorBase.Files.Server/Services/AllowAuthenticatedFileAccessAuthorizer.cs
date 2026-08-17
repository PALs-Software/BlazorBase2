using System.Security.Claims;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Insecure opt-in <see cref="IFileAccessAuthorizer"/> that permits any authenticated principal
/// to access or claim ownership of any file. It is <strong>not</strong> registered by default
/// (see <see cref="OwnerScopedFileAccessAuthorizer"/>); it exists only for single-tenant or
/// trusted deployments where every authenticated user is equally entitled to every file. Hosts
/// that need this permissive posture must opt in explicitly via
/// <c>AddBlazorBaseFileAccessAuthorizer&lt;AllowAuthenticatedFileAccessAuthorizer&gt;()</c>.
/// </summary>
public class AllowAuthenticatedFileAccessAuthorizer : IFileAccessAuthorizer
{
    /// <inheritdoc/>
    public Task<bool> CanAccessAsync(Guid fileId, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Identity?.IsAuthenticated == true);

    /// <inheritdoc/>
    public Task<bool> CanAssignScopeAsync(string? ownerScopeKey, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Identity?.IsAuthenticated == true);
}
