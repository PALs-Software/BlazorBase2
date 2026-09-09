namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>Fixed tenant identifiers the fake provider, the harness host and its probes all agree on.</summary>
public static class FakeIdentityProviderTenants
{
    /// <summary>The tenant the harness's <c>External</c> profile allow-lists.</summary>
    public const string AllowedTenantId = "22222222-2222-2222-2222-222222222222";

    /// <summary>A real-looking tenant the allow-list deliberately does not contain.</summary>
    public const string DisallowedTenantId = "33333333-3333-3333-3333-333333333333";

    /// <summary>The path segment a multi-tenant "/common" authority publishes a tenant-templated issuer under.</summary>
    public const string MultiTenantSegment = "common";
}
