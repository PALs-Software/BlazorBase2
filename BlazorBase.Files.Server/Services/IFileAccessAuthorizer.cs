using System.Security.Claims;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Determines whether the acting principal may access a specific file. The default implementation
/// (<see cref="OwnerScopedFileAccessAuthorizer"/>) grants access only when the file's
/// <see cref="BlazorBase.Files.Models.BaseFile.OwnerScopeKey"/> equals the caller's
/// <c>NameIdentifier</c>. Hosts that partition files differently (e.g. by a tenant identifier)
/// register a custom implementation via <c>AddBlazorBaseFileAccessAuthorizer&lt;T&gt;()</c>;
/// <see cref="AllowAuthenticatedFileAccessAuthorizer"/> remains available as an explicit,
/// intentionally permissive opt-in for single-tenant/trusted deployments.
/// </summary>
public interface IFileAccessAuthorizer
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="user"/> is allowed to access the
    /// file identified by <paramref name="fileId"/>.
    /// </summary>
    /// <param name="fileId">The stable identifier of the file being accessed.</param>
    /// <param name="user">The principal making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> CanAccessAsync(Guid fileId, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="user"/> is allowed to assign the
    /// supplied <paramref name="ownerScopeKey"/> to a newly uploaded file. Hosts that partition
    /// files by owner scope must reject a client-supplied scope the principal does not own,
    /// otherwise a caller could plant files into a foreign scope. The default implementation
    /// permits a caller to assign only a null scope (defaulted to the uploader) or their own
    /// <c>NameIdentifier</c>, and rejects claiming another principal's scope.
    /// </summary>
    /// <param name="ownerScopeKey">The scope key the upload requests, or <see langword="null"/> for an unscoped file.</param>
    /// <param name="user">The principal making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> CanAssignScopeAsync(string? ownerScopeKey, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the owner scope to store when the caller did not supply one on upload. The default
    /// implementation returns <see langword="null"/> (unscoped), matching the permissive posture of
    /// <see cref="AllowAuthenticatedFileAccessAuthorizer"/>. Owner-scoping implementations (such as
    /// <see cref="OwnerScopedFileAccessAuthorizer"/>) override this to return the caller's own
    /// scope identifier so uploads are owned by default.
    /// </summary>
    /// <param name="user">The principal making the request.</param>
    string? ResolveDefaultOwnerScope(ClaimsPrincipal user) => null;
}
