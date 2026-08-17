using BlazorBase.User.Server.Entities;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// Validates <see cref="AccessTokenSettings"/> at application startup, so a prefix that cannot fit
/// the column fails during startup rather than when the first token is minted.
/// </summary>
/// <remarks>
/// Without this check the failure mode is nasty and late: the stored
/// <see cref="AccessToken.Prefix"/> is the configured prefix plus a fixed body slice, so an
/// over-long prefix is silently truncated on SQL Server (or accepted whole on a provider that does
/// not enforce lengths, then never matched again). Either way the token authenticates once, if at
/// all, and the operator sees "invalid token" with nothing in the logs pointing at configuration.
/// </remarks>
public class AccessTokenSettingsValidator : IValidateOptions<AccessTokenSettings>
{
    /// <summary>
    /// Characters appended to the configured prefix to form the stored lookup key. Must stay in
    /// step with <c>AccessTokenSecretGenerator.PrefixBodyLength</c>.
    /// </summary>
    private const int PrefixBodyLength = 8;

    /// <summary>The <c>MaxLength</c> declared on <see cref="AccessToken.Prefix"/>.</summary>
    private const int PrefixColumnLength = 32;

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, AccessTokenSettings options)
    {
        if (string.IsNullOrEmpty(options.SecretPrefix))
            return ValidateOptionsResult.Fail("AccessTokenSettings.SecretPrefix must not be empty - it is what makes a leaked secret identifiable as this application's token.");

        var storedPrefixLength = options.SecretPrefix.Length + PrefixBodyLength;

        if (storedPrefixLength > PrefixColumnLength)
            return ValidateOptionsResult.Fail(
                $"AccessTokenSettings.SecretPrefix is too long: '{options.SecretPrefix}' plus the {PrefixBodyLength}-character lookup body needs {storedPrefixLength} characters, but AccessToken.Prefix holds at most {PrefixColumnLength}.");

        return ValidateOptionsResult.Success;
    }
}
