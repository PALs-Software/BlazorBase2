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
