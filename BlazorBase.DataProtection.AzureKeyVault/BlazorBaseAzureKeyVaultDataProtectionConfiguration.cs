using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BlazorBase.DataProtection.AzureKeyVault;

/// <summary>
/// Registers an opt-in, Azure Key Vault backed configuration of ASP.NET Core Data Protection for BlazorBase.
/// This is NOT applied by default by <c>AddBlazorBaseDataProtection</c> - call this extension explicitly
/// instead, when the data protection key ring should be persisted in the application's database and wrapped
/// at rest by an Azure Key Vault key instead of the local machine/user profile.
/// </summary>
public static class BlazorBaseAzureKeyVaultDataProtectionConfiguration
{
    /// <summary>
    /// Configures ASP.NET Core Data Protection to persist its key ring in <typeparamref name="TDbContext"/>
    /// (via <see cref="IDataProtectionKeyContext"/>) and to protect the key ring at rest with an Azure Key Vault key.
    /// Goes through <see cref="BlazorBaseDataProtectionServiceCollectionExtensions.AddBlazorBaseDataProtection"/>
    /// so there remains a single Data Protection entry point for the host to call.
    /// </summary>
    /// <typeparam name="TDbContext">
    /// The application's <see cref="DbContext"/>, which must also implement <see cref="IDataProtectionKeyContext"/>
    /// (add a <c>DbSet&lt;DataProtectionKey&gt;</c> and an EF Core migration for it).
    /// </typeparam>
    public static IServiceCollection AddBlazorBaseAzureKeyVaultDataProtection<TDbContext>(this IServiceCollection serviceCollection, Action<BlazorBaseAzureKeyVaultDataProtectionSettings> configureSettings)
        where TDbContext : DbContext, IDataProtectionKeyContext
    {
        ArgumentNullException.ThrowIfNull(configureSettings);

        var settings = new BlazorBaseAzureKeyVaultDataProtectionSettings();
        configureSettings(settings);

        if (String.IsNullOrWhiteSpace(settings.ApplicationName))
            throw new ArgumentException($"{nameof(BlazorBaseAzureKeyVaultDataProtectionSettings)}.{nameof(BlazorBaseAzureKeyVaultDataProtectionSettings.ApplicationName)} must be set.", nameof(configureSettings));

        if (settings.AzureKeyVaultKeyIdentifier == null)
            throw new ArgumentException($"{nameof(BlazorBaseAzureKeyVaultDataProtectionSettings)}.{nameof(BlazorBaseAzureKeyVaultDataProtectionSettings.AzureKeyVaultKeyIdentifier)} must be set.", nameof(configureSettings));

        var credential = settings.TokenCredential ?? new DefaultAzureCredential();

        return serviceCollection.AddBlazorBaseDataProtection(builder =>
        {
            builder
                .SetApplicationName(settings.ApplicationName)
                .PersistKeysToDbContext<TDbContext>()
                .ProtectKeysWithAzureKeyVault(settings.AzureKeyVaultKeyIdentifier, credential);

            settings.Configure?.Invoke(builder);
        });
    }
}
