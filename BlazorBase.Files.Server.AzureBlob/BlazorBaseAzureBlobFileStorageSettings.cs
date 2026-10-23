using Azure.Core;

namespace BlazorBase.Files.Server.AzureBlob;

/// <summary>
/// Settings for the opt-in Azure Blob Storage file provider. Either <see cref="ServiceUri"/> (token
/// authentication, for Azure) or <see cref="ConnectionString"/> (Azurite / local development) must be set.
/// </summary>
public class BlazorBaseAzureBlobFileStorageSettings
{
    /// <summary>
    /// The blob service endpoint, e.g. <c>https://myaccount.blob.core.windows.net</c>. Authenticated with
    /// <see cref="TokenCredential"/>. Mutually exclusive with <see cref="ConnectionString"/>.
    /// </summary>
    public Uri? ServiceUri { get; set; }

    /// <summary>
    /// The credential used together with <see cref="ServiceUri"/>. Defaults to
    /// <see cref="Azure.Identity.DefaultAzureCredential"/> (managed identity on Azure, developer sign-in locally)
    /// when left <see langword="null"/>.
    /// </summary>
    public TokenCredential? TokenCredential { get; set; }

    /// <summary>
    /// A storage connection string, intended for Azurite and local development. Mutually exclusive with
    /// <see cref="ServiceUri"/>. Do not use shared-key connection strings in production.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The name of the container that holds permanent and temporary files. Required.
    /// </summary>
    public string ContainerName { get; set; } = "files";

    /// <summary>
    /// The blob name prefix of temporary uploads. A trailing <c>/</c> is added when missing. Permanent files
    /// are stored at the container root, so the prefix must not be empty.
    /// </summary>
    public string TemporaryPrefix { get; set; } = "temp/";

    /// <summary>
    /// When <see langword="true"/> the container is created on first use if it does not exist. Leave
    /// <see langword="false"/> when the container is provisioned by infrastructure and the application identity
    /// only holds data-plane permissions on it.
    /// </summary>
    public bool CreateContainerIfMissing { get; set; }

    internal void Validate()
    {
        var hasServiceUri = ServiceUri is not null;
        var hasConnectionString = !string.IsNullOrWhiteSpace(ConnectionString);

        if (hasServiceUri == hasConnectionString)
            throw new InvalidOperationException($"Exactly one of {nameof(ServiceUri)} and {nameof(ConnectionString)} must be set for the Azure Blob file storage.");

        if (string.IsNullOrWhiteSpace(ContainerName))
            throw new InvalidOperationException($"{nameof(ContainerName)} must be set for the Azure Blob file storage.");

        if (string.IsNullOrWhiteSpace(TemporaryPrefix.Trim('/')))
            throw new InvalidOperationException($"{nameof(TemporaryPrefix)} must not be empty for the Azure Blob file storage.");
    }

    internal string GetNormalizedTemporaryPrefix() => TemporaryPrefix.Trim().TrimStart('/').TrimEnd('/') + "/";
}
