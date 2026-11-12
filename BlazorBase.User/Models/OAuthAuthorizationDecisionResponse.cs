namespace BlazorBase.User.Models;

/// <summary>
/// Where the consent page sends the browser after the decision: the client's registered redirect URI
/// carrying either <c>code</c> or <c>error=access_denied</c>, plus <c>state</c> and <c>iss</c>.
/// </summary>
public class OAuthAuthorizationDecisionResponse
{
    public string RedirectUri { get; set; } = string.Empty;
}
