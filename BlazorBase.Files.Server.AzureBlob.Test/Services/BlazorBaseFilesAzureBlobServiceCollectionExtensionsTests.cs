using BlazorBase.Files.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Files.Server.AzureBlob.Test.Services;

public sealed class BlazorBaseFilesAzureBlobServiceCollectionExtensionsTests
{
    private const string DevelopmentConnectionString = "UseDevelopmentStorage=true";

    private sealed class EmptyDbContext(DbContextOptions<EmptyDbContext> options) : DbContext(options);

    private static ServiceCollection CreateCoreServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBaseFilesServer<EmptyDbContext>();
        return services;
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void CoreRegistration_UsesFileSystemProviderByDefault()
    {
        using var provider = CreateCoreServices().BuildServiceProvider();

        Assert.IsType<FileSystemFileStorage>(provider.GetRequiredService<IFileStorage>());
        Assert.IsType<FileSystemTemporaryFileStoreCleaner>(provider.GetRequiredService<ITemporaryFileStoreCleaner>());
    }

    [Fact]
    public void AddBlazorBaseFilesAzureBlobStorage_ReplacesStorageAndCleanerOnly()
    {
        var services = CreateCoreServices();
        var descriptorCountBefore = services.Count;

        services.AddBlazorBaseFilesAzureBlobStorage(settings => settings.ConnectionString = DevelopmentConnectionString);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<BlobFileStorage>(provider.GetRequiredService<IFileStorage>());
        Assert.IsType<BlobTemporaryFileStoreCleaner>(provider.GetRequiredService<ITemporaryFileStoreCleaner>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IFileStorage));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ITemporaryFileStoreCleaner));
        Assert.Equal(descriptorCountBefore + 1, services.Count);
    }

    [Fact]
    public void AddBlazorBaseFilesAzureBlobStorage_ServiceUriWithoutCredential_IsAccepted()
    {
        var services = CreateCoreServices();

        services.AddBlazorBaseFilesAzureBlobStorage(settings => settings.ServiceUri = new Uri("https://account.blob.core.windows.net"));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<BlobFileStorage>(provider.GetRequiredService<IFileStorage>());
    }

    [Fact]
    public void AddBlazorBaseFilesAzureBlobStorage_NeitherServiceUriNorConnectionString_Throws()
    {
        var services = CreateCoreServices();

        Assert.Throws<InvalidOperationException>(() => services.AddBlazorBaseFilesAzureBlobStorage(_ => { }));
    }

    [Fact]
    public void AddBlazorBaseFilesAzureBlobStorage_BothServiceUriAndConnectionString_Throws()
    {
        var services = CreateCoreServices();

        Assert.Throws<InvalidOperationException>(() => services.AddBlazorBaseFilesAzureBlobStorage(settings =>
        {
            settings.ServiceUri = new Uri("https://account.blob.core.windows.net");
            settings.ConnectionString = DevelopmentConnectionString;
        }));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddBlazorBaseFilesAzureBlobStorage_EmptyContainerName_Throws(string containerName)
    {
        var services = CreateCoreServices();

        Assert.Throws<InvalidOperationException>(() => services.AddBlazorBaseFilesAzureBlobStorage(settings =>
        {
            settings.ConnectionString = DevelopmentConnectionString;
            settings.ContainerName = containerName;
        }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void AddBlazorBaseFilesAzureBlobStorage_EmptyTemporaryPrefix_Throws(string prefix)
    {
        var services = CreateCoreServices();

        Assert.Throws<InvalidOperationException>(() => services.AddBlazorBaseFilesAzureBlobStorage(settings =>
        {
            settings.ConnectionString = DevelopmentConnectionString;
            settings.TemporaryPrefix = prefix;
        }));
    }

    [Theory]
    [InlineData("temp", "temp/")]
    [InlineData("temp/", "temp/")]
    [InlineData("/uploads/tmp", "uploads/tmp/")]
    public void AzureBlobContainerProvider_NormalizesTemporaryPrefix(string configured, string expected)
    {
        var provider = new AzureBlobContainerProvider(new BlazorBaseAzureBlobFileStorageSettings
        {
            ConnectionString = DevelopmentConnectionString,
            TemporaryPrefix = configured,
        });

        Assert.Equal(expected, provider.TemporaryPrefix);
    }

    [Fact]
    public void AddBlazorBaseFilesStorageProvider_NoProviderConfigured_KeepsFileSystem()
    {
        var services = CreateCoreServices();

        services.AddBlazorBaseFilesStorageProvider(BuildConfiguration([]));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<FileSystemFileStorage>(provider.GetRequiredService<IFileStorage>());
    }

    [Fact]
    public void AddBlazorBaseFilesStorageProvider_FileSystem_KeepsFileSystem()
    {
        var services = CreateCoreServices();

        services.AddBlazorBaseFilesStorageProvider(BuildConfiguration(new() { ["Files:Provider"] = "FileSystem" }));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<FileSystemFileStorage>(provider.GetRequiredService<IFileStorage>());
        Assert.IsType<FileSystemTemporaryFileStoreCleaner>(provider.GetRequiredService<ITemporaryFileStoreCleaner>());
    }

    [Fact]
    public void AddBlazorBaseFilesStorageProvider_AzureBlob_BindsSettingsAndReplacesStorage()
    {
        var services = CreateCoreServices();
        var configuration = BuildConfiguration(new()
        {
            ["Files:Provider"] = "azureblob",
            ["Files:AzureBlob:ConnectionString"] = DevelopmentConnectionString,
            ["Files:AzureBlob:ContainerName"] = "site-files",
            ["Files:AzureBlob:TemporaryPrefix"] = "incoming",
        });

        services.AddBlazorBaseFilesStorageProvider(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<BlobFileStorage>(provider.GetRequiredService<IFileStorage>());
        Assert.Equal("incoming/", provider.GetRequiredService<AzureBlobContainerProvider>().TemporaryPrefix);
    }

    [Fact]
    public void AddBlazorBaseFilesStorageProvider_AzureBlobWithoutSettings_Throws()
    {
        var services = CreateCoreServices();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddBlazorBaseFilesStorageProvider(BuildConfiguration(new() { ["Files:Provider"] = "AzureBlob" })));
    }

    [Fact]
    public void AddBlazorBaseFilesStorageProvider_UnknownProvider_Throws()
    {
        var services = CreateCoreServices();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddBlazorBaseFilesStorageProvider(BuildConfiguration(new() { ["Files:Provider"] = "Ftp" })));
    }
}
