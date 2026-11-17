using System.ComponentModel.DataAnnotations;

namespace BlazorBase.User.Models;

/// <summary>The user's answer to a pending OAuth authorization request (<c>POST api/oauth/authorization-requests/decision</c>).</summary>
public class OAuthAuthorizationDecisionRequest
{
    /// <summary>The protected request the consent page received in its <c>request</c> query parameter.</summary>
    [Required]
    public string Request { get; set; } = string.Empty;

    public bool Approved { get; set; }
}
