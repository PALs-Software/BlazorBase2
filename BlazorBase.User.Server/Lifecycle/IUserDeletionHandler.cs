namespace BlazorBase.User.Server.Lifecycle;

/// <summary>
/// A generic seam for host-specific cleanup that must run after an Identity user has been deleted —
/// for example revoking external OAuth tokens or deleting related rows that have no database-level
/// cascade to the Identity user table. Register one or more implementations in the host's DI container;
/// <see cref="BlazorBase.User.Server.Services.UserDataProvider{TUser}"/> resolves them as
/// <see cref="IEnumerable{T}"/> and invokes every registered handler once per successful deletion.
/// </summary>
public interface IUserDeletionHandler
{
    /// <summary>
    /// Invoked once per registered handler, after the Identity user row has been successfully deleted.
    /// Implementations are best-effort: they must not throw for recoverable failures, since the user
    /// is already gone and a thrown exception here would surface as a spurious error to the caller.
    /// </summary>
    /// <param name="userId">The id of the user that was just deleted.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    Task OnUserDeletedAsync(string userId, CancellationToken cancellationToken = default);
}
