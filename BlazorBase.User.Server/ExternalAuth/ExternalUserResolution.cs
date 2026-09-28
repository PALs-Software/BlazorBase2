using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// The outcome of resolving an external identity to an application user. Rejection is a
/// first-class result rather than a thrown exception or a <see langword="null"/> return, so a
/// host expresses "this subject does not exist here" the same deliberate way it expresses
/// acceptance, and the exchange endpoint cannot mistake "not found" for "not yet implemented".
/// </summary>
public sealed class ExternalUserResolution<TUser> where TUser : BaseUser
{
    public bool IsAccepted { get; }
    public TUser? User { get; }
    public string? RejectionReason { get; }

    private ExternalUserResolution(bool isAccepted, TUser? user, string? rejectionReason)
    {
        IsAccepted = isAccepted;
        User = user;
        RejectionReason = rejectionReason;
    }

    /// <summary>The external identity resolves to <paramref name="user"/>.</summary>
    public static ExternalUserResolution<TUser> Accept(TUser user) => new(true, user, null);

    /// <summary>The external identity does not resolve to any user this host recognizes.</summary>
    public static ExternalUserResolution<TUser> Reject(string reason) => new(false, null, reason);
}
