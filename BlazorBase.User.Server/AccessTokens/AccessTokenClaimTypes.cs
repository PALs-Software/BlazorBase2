namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// Claim type constants used in the access-token authentication principal.
/// </summary>
public static class AccessTokenClaimTypes
{
    /// <summary>The claim carrying the access token's own unique identifier.</summary>
    public const string TokenId = "blazorbase:token_id";
}
