namespace BlazorBase.User.Services;

/// <summary>
/// Options for the BlazorBase.User client-side services. Register via
/// <see cref="BlazorBaseUserServiceCollectionExtensions.AddBlazorBaseUserClient"/> and configure
/// with <c>builder.Services.Configure&lt;BlazorBaseUserClientOptions&gt;(o =&gt; …)</c>.
/// </summary>
public sealed class BlazorBaseUserClientOptions
{
    /// <summary>
    /// When <see langword="true"/> the setup form on the lib-routed <c>/login</c> page renders an
    /// optional household-name field bound to <c>SetupRequest.HouseholdName</c>. Defaults to
    /// <see langword="false"/> so existing hosts that do not use households are unaffected.
    /// </summary>
    public bool CollectHouseholdNameOnSetup { get; set; }
}
