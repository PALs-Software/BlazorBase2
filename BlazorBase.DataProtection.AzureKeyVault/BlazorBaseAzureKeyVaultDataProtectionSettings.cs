using Azure.Core;
using System;

namespace BlazorBase.DataProtection.AzureKeyVault;

/// <summary>
/// Settings used to configure ASP.NET Core Data Protection with an Azure Key Vault protected key ring
/// that is persisted in an EF Core <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
/// </summary>
public class BlazorBaseAzureKeyVaultDataProtectionSettings
{
    /// <summary>
    /// The application name passed to <c>SetApplicationName</c>. This isolates the data protection key ring
    /// from other applications that might share the same key store. Required.
    /// </summary>
    public string ApplicationName { get; set; } = null!;

    /// <summary>
    /// The key identifier (URI) of the Azure Key Vault key used to protect the data protection key ring at rest,
    /// e.g. <c>https://my-vault.vault.azure.net/keys/dataprotection-key</c>. Do not include a specific key version
    /// so that Data Protection automatically uses the current key version after a rotation. Required.
    /// </summary>
    public Uri AzureKeyVaultKeyIdentifier { get; set; } = null!;

    /// <summary>
    /// The credential used to authenticate against Azure Key Vault. Defaults to <see cref="Azure.Identity.DefaultAzureCredential"/>
    /// (Managed Identity on Azure, falls back to developer sign-in locally) when left <see langword="null"/>.
    /// </summary>
    public TokenCredential? TokenCredential { get; set; }

    /// <summary>
    /// Optional raw hook applied last, after the standard application name, key persistence and Azure Key Vault
    /// protection have been configured. Use this for advanced scenarios that are not covered by this settings class.
    /// </summary>
    public Action<Microsoft.AspNetCore.DataProtection.IDataProtectionBuilder>? Configure { get; set; }
}
