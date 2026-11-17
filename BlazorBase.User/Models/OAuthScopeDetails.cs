namespace BlazorBase.User.Models;

/// <summary>One scope of an OAuth authorization request, with the description the server configured for it.</summary>
public class OAuthScopeDetails
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
