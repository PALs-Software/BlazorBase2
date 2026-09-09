using BlazorBase.DataProtection.AzureKeyVault;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace BlazorBase.DataProtection.Test;

public class BlazorBaseAzureKeyVaultDataProtectionConfigurationTests
{
    private static readonly Uri KeyIdentifier = new("https://example-vault.vault.azure.net/keys/dataprotection-key");

    private sealed class FakeDbContext(DbContextOptions<FakeDbContext> options)
        : DbContext(options), IDataProtectionKeyContext
    {
        public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    }

    [Fact]
    public void AddBlazorBaseAzureKeyVaultDataProtection_ThrowsWhenApplicationNameMissing()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddBlazorBaseAzureKeyVaultDataProtection<FakeDbContext>(settings =>
            {
                settings.AzureKeyVaultKeyIdentifier = KeyIdentifier;
            }));

        Assert.Contains(nameof(BlazorBaseAzureKeyVaultDataProtectionSettings.ApplicationName), exception.Message);
    }

    [Fact]
    public void AddBlazorBaseAzureKeyVaultDataProtection_ThrowsWhenKeyIdentifierMissing()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddBlazorBaseAzureKeyVaultDataProtection<FakeDbContext>(settings =>
            {
                settings.ApplicationName = "TestApp";
            }));

        Assert.Contains(nameof(BlazorBaseAzureKeyVaultDataProtectionSettings.AzureKeyVaultKeyIdentifier), exception.Message);
    }

    [Fact]
    public void AddBlazorBaseAzureKeyVaultDataProtection_InvokesConfigureEscapeHatch()
    {
        var services = new ServiceCollection();
        var invoked = false;
        IDataProtectionBuilder? capturedBuilder = null;

        services.AddBlazorBaseAzureKeyVaultDataProtection<FakeDbContext>(settings =>
        {
            settings.ApplicationName = "TestApp";
            settings.AzureKeyVaultKeyIdentifier = KeyIdentifier;
            settings.Configure = builder =>
            {
                invoked = true;
                capturedBuilder = builder;
            };
        });

        Assert.True(invoked);
        Assert.NotNull(capturedBuilder);
    }

    [Fact]
    public void AddBlazorBaseAzureKeyVaultDataProtection_RegistrationResolvesDataProtectionProvider()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseAzureKeyVaultDataProtection<FakeDbContext>(settings =>
        {
            settings.ApplicationName = "TestApp";
            settings.AzureKeyVaultKeyIdentifier = KeyIdentifier;
        });

        var serviceProvider = services.BuildServiceProvider();
        var dataProtectionProvider = serviceProvider.GetService<IDataProtectionProvider>();

        Assert.NotNull(dataProtectionProvider);
    }
}
