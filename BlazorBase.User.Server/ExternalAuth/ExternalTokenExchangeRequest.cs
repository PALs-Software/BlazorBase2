namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>Request body for the external-token exchange endpoint.</summary>
public class ExternalTokenExchangeRequest
{
    public string ExternalToken { get; set; } = string.Empty;
}
