namespace BlazorBase.Files.Server.AzureBlob;

/// <summary>
/// The file storage provider a host selects through configuration (<c>Files:Provider</c>).
/// </summary>
public enum BlazorBaseFilesProvider
{
    /// <summary>
    /// The default provider: files are stored in the local file system.
    /// </summary>
    FileSystem = 0,

    /// <summary>
    /// Files are stored in Azure Blob Storage.
    /// </summary>
    AzureBlob = 1,
}
