using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.DataProtection.Test;

public class BlazorBaseDataProtectionServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBlazorBaseDataProtection_InvokesConfigureDelegate()
    {
        var services = new ServiceCollection();
        var invoked = false;
        IDataProtectionBuilder? capturedBuilder = null;

        services.AddBlazorBaseDataProtection(builder =>
        {
            invoked = true;
            capturedBuilder = builder;
        });

        Assert.True(invoked);
        Assert.NotNull(capturedBuilder);
    }

    [Fact]
    public void AddBlazorBaseDataProtection_WithoutConfigure_StillRegistersProvider()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseDataProtection();

        var serviceProvider = services.BuildServiceProvider();
        var dataProtectionProvider = serviceProvider.GetService<IDataProtectionProvider>();

        Assert.NotNull(dataProtectionProvider);
    }

    [Fact]
    public void AddBlazorBaseDataProtection_HonorsHostKeyPersistence()
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory("BlazorBaseDataProtectionTests");

        try
        {
            var services = new ServiceCollection();

            services.AddBlazorBaseDataProtection(builder => builder.PersistKeysToFileSystem(temporaryDirectory));

            var serviceProvider = services.BuildServiceProvider();
            var keyManagementOptions = serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>();

            Assert.NotNull(keyManagementOptions.Value.XmlRepository);
            Assert.IsType<FileSystemXmlRepository>(keyManagementOptions.Value.XmlRepository);
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }
}
