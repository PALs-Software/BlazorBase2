namespace BlazorBase.User.Models;

/// <summary>
/// Answers <c>2fa/disable</c>, which used to return no body at all - disabling two-factor rotates the
/// security stamp, so a body is now needed to carry <see cref="RefreshedTokens"/>; see
/// <see cref="TwoFactorStatusResponse.RefreshedTokens"/> for why a client must adopt it.
/// </summary>
public class TwoFactorDisableResponse
{
    public LoginResponse? RefreshedTokens { get; set; }
}
