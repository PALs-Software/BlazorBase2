namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>A thread-safe on/off switch a probe flips to take <see cref="FakeIdentityProviderHost"/> offline without restarting it.</summary>
internal sealed class FakeIdentityProviderAvailability
{
    private volatile bool IsOnlineField = true;

    public bool IsOnline => IsOnlineField;

    public void SetOnline(bool online) => IsOnlineField = online;
}
