namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// Configuration options for the access-token subsystem, bound from the <c>AccessTokenSettings</c>
/// section of application configuration.
/// </summary>
public class AccessTokenSettings
{
    /// <summary>
    /// The literal marker every secret starts with, so a leaked string is recognizable as a token
    /// of this application at a glance (and by secret scanners). Pick something short and unique
    /// per application, e.g. <c>myapp_</c>.
    /// </summary>
    /// <remarks>
    /// Changing the <b>length</b> of this value invalidates every token already issued: the stored
    /// <see cref="Entities.AccessToken.Prefix"/> is a fixed-length slice taken by position, so a
    /// prefix of a different length no longer lines up with what was persisted and every lookup
    /// misses. Changing it to a different value of the same length is equally breaking for tokens
    /// already in circulation. Treat it as set-once per application.
    /// </remarks>
    public string SecretPrefix { get; set; } = "bbat_";

    /// <summary>
    /// Random bytes behind each secret. Values below 32 are raised to 32 - a shorter secret is
    /// never what the caller actually wants, and silently honouring it would weaken every token.
    /// </summary>
    public int SecretByteLength { get; set; } = 32;

    public int BruteForceBaseDelayMs { get; set; } = 200;

    public int BruteForceMaxDelayMs { get; set; } = 10000;
}
