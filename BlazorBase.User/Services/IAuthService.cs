using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

public interface IAuthService
{
    Task<AuthStatusResponse> GetStatusAsync();
    Task<LoginResponse> SetupAsync(SetupRequest request);

    /// <summary>
    /// Returns a full token pair for a login that needs no second factor, or the pending token to
    /// complete with <see cref="LoginTwoFactorAsync"/> when the account requires one.
    /// </summary>
    Task<AuthServiceLoginOutcome> LoginAsync(LoginRequest request);

    /// <summary>
    /// Completes a login that <see cref="LoginAsync"/> answered with a pending token.
    /// </summary>
    Task<LoginResponse> LoginTwoFactorAsync(TwoFactorLoginRequest request);

    Task<LoginResponse> RefreshAsync(string refreshToken);

    /// <summary>
    /// Posts the logout. Completes only when the server answered with a success status (the endpoint is
    /// idempotent, so an unknown token is a success too); any other status (for example <c>403</c> for a missing
    /// antiforgery token or <c>5xx</c>) throws <see cref="HttpRequestException"/>, because a logout the server did
    /// not confirm leaves the session alive. A network failure throws as well.
    /// </summary>
    Task LogoutAsync(string refreshToken);

    /// <summary>
    /// Signs in as the server's configured development account. Returns null when the server
    /// does not offer that path, which is the case in every environment but Development.
    /// </summary>
    Task<LoginResponse?> DevelopmentLoginAsync();

    /// <summary>
    /// Asks the server who the caller currently is. The only way to obtain the claims under either
    /// transport, since the access token is opaque to the client.
    /// </summary>
    /// <param name="accessToken">
    /// Attached as a bearer header for this one call. Required under the bearer transport, where
    /// nothing else identifies the caller: this client deliberately carries no
    /// <see cref="AuthTokenHandler"/>, because that handler resolves <see cref="IAuthService"/> to
    /// refresh and attaching it here would close the cycle. Leave it null under the cookie transport,
    /// where the session cookie already identifies the caller.
    /// </param>
    Task<AuthSessionResponse> GetSessionAsync(string? accessToken = null);

    Task ConfirmEmailAsync(ConfirmEmailRequest request);
    Task ResendConfirmationAsync(ResendConfirmationRequest request);
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>
    /// Changes the signed-in account's own password through <c>POST api/auth/manage/change-password</c>. A
    /// refusal the server explains with a <c>400</c> (wrong current password, a new password the rules
    /// reject, an account without a password) comes back as a <see cref="ChangePasswordOutcome"/> that is not
    /// <see cref="ChangePasswordOutcome.Succeeded"/>; any other failure (<c>401</c>, <c>429</c>, <c>5xx</c>, a
    /// network error) throws <see cref="HttpRequestException"/>. Under the bearer transport the replacement
    /// token pair of the response is stored, so the caller stays signed in; under the cookie transport the
    /// server has already rewritten the session cookie.
    /// </summary>
    Task<ChangePasswordOutcome> ChangePasswordAsync(ChangePasswordRequest request);

    Task<TwoFactorStatusResponse> GetTwoFactorStatusAsync();
    Task<TwoFactorRecoveryCodesResponse> EnableTwoFactorAsync(TwoFactorEnableRequest request);
    Task DisableTwoFactorAsync(TwoFactorVerificationRequest request);
    Task<TwoFactorRecoveryCodesResponse> RegenerateRecoveryCodesAsync(TwoFactorVerificationRequest request);
}

/// <summary>
/// Either a completed login (<see cref="Tokens"/>) or a second factor requirement
/// (<see cref="PendingToken"/>) - never both.
/// </summary>
public sealed class AuthServiceLoginOutcome
{
    public LoginResponse? Tokens { get; init; }
    public string? PendingToken { get; init; }

    public bool RequiresTwoFactor => PendingToken is not null;

    public static AuthServiceLoginOutcome Completed(LoginResponse tokens) => new() { Tokens = tokens };
    public static AuthServiceLoginOutcome TwoFactorRequired(string pendingToken) => new() { PendingToken = pendingToken };
}
