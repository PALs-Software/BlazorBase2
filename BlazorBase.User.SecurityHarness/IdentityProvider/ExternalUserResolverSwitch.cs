namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>Which <see cref="BlazorBase.User.Server.ExternalAuth.IExternalUserResolver{TUser}"/> <see cref="SwitchingExternalUserResolver"/> delegates to.</summary>
public enum ExternalUserResolverMode
{
    /// <summary>Matches by the provider's subject/<c>oid</c> claim, rejecting an unknown subject.</summary>
    Strict,

    /// <summary>Matches by the <c>email</c> claim alone - see <see cref="NaiveEmailMatchingExternalUserResolver"/>.</summary>
    Naive,
}

/// <summary>
/// A mutable switch a probe flips to change which resolver <see cref="SwitchingExternalUserResolver"/>
/// delegates to, without restarting the <c>External</c> profile's host.
/// </summary>
public sealed class ExternalUserResolverSwitch
{
    private volatile ExternalUserResolverMode ModeField = ExternalUserResolverMode.Strict;

    public ExternalUserResolverMode Mode
    {
        get => ModeField;
        set => ModeField = value;
    }
}
