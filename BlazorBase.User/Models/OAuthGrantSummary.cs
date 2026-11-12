namespace BlazorBase.User.Models;

/// <summary>One app the current user has connected through OAuth (<c>GET api/oauth/grants</c>).</summary>
public class OAuthGrantSummary
{
    public Guid Id { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public string RedirectHost { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }
}
