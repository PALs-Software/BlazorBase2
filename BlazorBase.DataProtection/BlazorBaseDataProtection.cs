using Microsoft.AspNetCore.DataProtection;
using System.Collections.Concurrent;

namespace BlazorBase.DataProtection;

public static class BlazorBaseDataProtection
{
    public const string ProtectorPurpose = "BlazorBase.DataProtection.StringEncryption.v1";

    private static IDataProtector? Protector;
    private static IDataProtectionProvider? Provider;
    private static readonly ConcurrentDictionary<string, IDataProtector> PurposedProtectors = new();

    public static bool IsInitialized => Protector is not null;

    public static void Initialize(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        Provider = provider;
        Protector = provider.CreateProtector(ProtectorPurpose);
        PurposedProtectors.Clear();
    }

    public static string Protect(string plainText)
    {
        if (Protector is null)
            throw new InvalidOperationException("BlazorBaseDataProtection has not been initialized. Call app.Services.UseBlazorBaseDataProtection() on application startup.");

        return Protector.Protect(plainText);
    }

    public static string Unprotect(string protectedText)
    {
        if (Protector is null)
            throw new InvalidOperationException("BlazorBaseDataProtection has not been initialized. Call app.Services.UseBlazorBaseDataProtection() on application startup.");

        return Protector.Unprotect(protectedText);
    }

    /// <summary>
    /// Encrypts <paramref name="plainText"/> using a protector scoped to <paramref name="purpose"/>, deriving a
    /// key that is distinct from the default no-purpose protector and from every other purpose.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <param name="purpose">The purpose the resulting ciphertext is scoped to.</param>
    /// <returns>The purpose-scoped ciphertext.</returns>
    public static string Protect(string plainText, string purpose)
    {
        var purposedProtector = GetPurposedProtector(purpose);

        return purposedProtector.Protect(plainText);
    }

    /// <summary>
    /// Decrypts <paramref name="protectedText"/> using a protector scoped to <paramref name="purpose"/>. Ciphertext
    /// produced under a different purpose (or the default no-purpose protector) fails to decrypt.
    /// </summary>
    /// <param name="protectedText">The purpose-scoped ciphertext to decrypt.</param>
    /// <param name="purpose">The purpose the ciphertext was scoped to.</param>
    /// <returns>The decrypted plain text.</returns>
    public static string Unprotect(string protectedText, string purpose)
    {
        var purposedProtector = GetPurposedProtector(purpose);

        return purposedProtector.Unprotect(protectedText);
    }

    private static IDataProtector GetPurposedProtector(string purpose)
    {
        if (string.IsNullOrEmpty(purpose))
            throw new ArgumentException("Purpose must not be null or empty.", nameof(purpose));

        var provider = Provider;

        if (provider is null)
            throw new InvalidOperationException("BlazorBaseDataProtection has not been initialized. Call app.Services.UseBlazorBaseDataProtection() on application startup.");

        return PurposedProtectors.GetOrAdd(purpose, key => provider.CreateProtector(ProtectorPurpose, key));
    }

    internal static void ResetForTesting()
    {
        Protector = null;
        Provider = null;
        PurposedProtectors.Clear();
    }
}
