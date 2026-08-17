using System.Security.Cryptography;

namespace BlazorBase.DataProtection;

public static class StringExtensions
{
    public static string EncryptString(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return BlazorBaseDataProtection.Protect(input);
    }

    public static string? DecryptStringToInsecureString(this string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            return null;

        try
        {
            return BlazorBaseDataProtection.Unprotect(encryptedData);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Encrypts <paramref name="input"/> using a protector scoped to <paramref name="purpose"/>.
    /// </summary>
    /// <param name="input">The plain text to encrypt.</param>
    /// <param name="purpose">The purpose the resulting ciphertext is scoped to.</param>
    /// <returns>The purpose-scoped ciphertext, or <paramref name="input"/> unchanged when null or empty.</returns>
    public static string EncryptString(this string input, string purpose)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return BlazorBaseDataProtection.Protect(input, purpose);
    }

    /// <summary>
    /// Decrypts <paramref name="encryptedData"/> using a protector scoped to <paramref name="purpose"/>.
    /// </summary>
    /// <param name="encryptedData">The purpose-scoped ciphertext to decrypt.</param>
    /// <param name="purpose">The purpose the ciphertext was scoped to.</param>
    /// <returns>The decrypted plain text, or <c>null</c> when the input is null/empty or decryption fails.</returns>
    public static string? DecryptStringToInsecureString(this string encryptedData, string purpose)
    {
        if (string.IsNullOrEmpty(purpose))
            throw new ArgumentException("Purpose must not be null or empty.", nameof(purpose));

        if (string.IsNullOrEmpty(encryptedData))
            return null;

        try
        {
            return BlazorBaseDataProtection.Unprotect(encryptedData, purpose);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
