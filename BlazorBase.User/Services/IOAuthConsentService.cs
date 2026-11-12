using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

/// <summary>
/// Client for the OAuth authorization server's user-facing API: the consent page reads and answers a
/// pending authorization request, the connected-apps panel lists and revokes the user's grants. Every
/// call is made as the signed-in user, so the host registers it like <see cref="IUserService"/> - with
/// <see cref="AuthTokenHandler"/> under the bearer transport, with the cookie-session handlers under the
/// cookie transport.
/// </summary>
public interface IOAuthConsentService
{
    /// <summary>
    /// Returns what the consent page shows about <paramref name="request"/>, or <see langword="null"/>
    /// when the request is invalid or has expired. A <c>403</c> (the account may not authorize apps)
    /// surfaces as an <see cref="HttpRequestException"/> with <see cref="HttpRequestException.StatusCode"/> set.
    /// </summary>
    Task<OAuthAuthorizationRequestDetails?> GetAuthorizationRequestAsync(string request);

    /// <summary>
    /// Approves or denies the pending request and returns the client redirect to navigate to, or
    /// <see langword="null"/> when the request is invalid or has expired.
    /// </summary>
    Task<OAuthAuthorizationDecisionResponse?> SubmitDecisionAsync(OAuthAuthorizationDecisionRequest decision);

    /// <summary>Returns the apps the signed-in user has connected, newest first.</summary>
    Task<IReadOnlyList<OAuthGrantSummary>> GetGrantsAsync();

    /// <summary>Disconnects one of the signed-in user's apps; its tokens stop working immediately.</summary>
    Task RevokeGrantAsync(Guid grantId);
}
