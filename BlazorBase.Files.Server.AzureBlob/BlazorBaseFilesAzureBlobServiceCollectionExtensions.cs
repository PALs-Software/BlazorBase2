using BlazorBase.Files.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBase.Files.Server.AzureBlob;

/// <summary>
/// Opt-in registration of the Azure Blob Storage file provider. Nothing here is applied by
/// <c>AddBlazorBaseFilesServer</c>; the file system provider stays the default.
/// </summary>
public static class BlazorBaseFilesAzureBlobServiceCollectionExtensions
{
    /// <summary>
    /// The default configuration section that selects the provider (<c>Files:Provider</c>) and holds the
    /// blob settings (<c>Files:AzureBlob</c>).
    /// </summary>
    public const string DefaultConfigurationSectionName = "Files";

    /// <summary>
    /// Replaces the <see cref="IFileStorage"/> and <see cref="ITemporaryFileStoreCleaner"/> registrations with the
    /// Azure Blob Storage implementations. Call after <c>AddBlazorBaseFilesServer</c>; nothing else of the core
    /// registration is touched.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureSettings">Configures the <see cref="BlazorBaseAzureBlobFileStorageSettings"/>.</param>
    public static IServiceCollection AddBlazorBaseFilesAzureBlobStorage(this IServiceCollection services, Action<BlazorBaseAzureBlobFileStorageSettings> configureSettings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureSettings);

        var settings = new BlazorBaseAzureBlobFileStorageSettings();
        configureSettings(settings);
        settings.Validate();

        services.Replace(ServiceDescriptor.Singleton(_ => new AzureBlobContainerProvider(settings)));
        services.Replace(ServiceDescriptor.Singleton<IFileStorage, BlobFileStorage>());
        services.Replace(ServiceDescriptor.Singleton<ITemporaryFileStoreCleaner, BlobTemporaryFileStoreCleaner>());

        return services;
    }

    /// <summary>
    /// Selects the file storage provider from configuration. <c>Files:Provider</c> is <c>FileSystem</c> (default,
    /// also when missing - nothing is changed) or <c>AzureBlob</c>, which binds <c>Files:AzureBlob</c> to
    /// <see cref="BlazorBaseAzureBlobFileStorageSettings"/> and calls
    /// <see cref="AddBlazorBaseFilesAzureBlobStorage"/>. Call after <c>AddBlazorBaseFilesServer</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="configureSettings">Optional hook applied after binding, e.g. to supply a <c>TokenCredential</c>.</param>
    /// <param name="sectionName">The configuration section name, <c>Files</c> by default.</param>
    public static IServiceCollection AddBlazorBaseFilesStorageProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<BlazorBaseAzureBlobFileStorageSettings>? configureSettings = null,
        string sectionName = DefaultConfigurationSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        var section = configuration.GetSection(sectionName);
        var providerValue = section["Provider"];

        if (string.IsNullOrWhiteSpace(providerValue))
            return services;

        if (!Enum.TryParse<BlazorBaseFilesProvider>(providerValue.Trim(), ignoreCase: true, out var provider) || !Enum.IsDefined(provider))
            throw new InvalidOperationException($"Unknown {sectionName}:Provider '{providerValue}'. Supported values: {string.Join(", ", Enum.GetNames<BlazorBaseFilesProvider>())}.");

        if (provider == BlazorBaseFilesProvider.FileSystem)
            return services;

        return services.AddBlazorBaseFilesAzureBlobStorage(settings =>
        {
            section.GetSection("AzureBlob").Bind(settings);
            configureSettings?.Invoke(settings);
        });
    }
}
