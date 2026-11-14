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

    /// <summary>
    /// The API path of the server's OAuth consent and connected-apps endpoints, relative to the
    /// <see cref="IOAuthConsentService"/> client's base address. Must match the server's
    /// <c>OAuthServer:EndpointBasePath</c>; defaults to the server default <c>api/oauth</c>.
    /// </summary>
    public string OAuthEndpointBasePath { get; set; } = "api/oauth";

    /// <summary>
    /// When <see langword="true"/> the lib-routed <c>/login</c> page links to <c>/forgot-password</c>. Turn it on
    /// only when the server maps <c>MapBlazorBaseAccountEndpoints</c> and sends mail; defaults to
    /// <see langword="false"/> so existing hosts are unaffected.
    /// </summary>
    public bool ShowForgotPasswordLink { get; set; }

    /// <summary>
    /// The API path of the server's invitation endpoints, relative to the <see cref="IUserInvitationService"/>
    /// client's base address. Must match the path given to <c>MapBlazorBaseUserInvitationEndpoints</c>;
    /// defaults to its default <c>api/auth/invitations</c>.
    /// </summary>
    public string InvitationEndpointBasePath { get; set; } = "api/auth/invitations";
}
