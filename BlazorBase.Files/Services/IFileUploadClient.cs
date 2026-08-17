using BlazorBase.Files.Models;

namespace BlazorBase.Files.Services;

/// <summary>
/// Client-side contract for uploading and deleting files via the server's file controller.
/// The implementation (<see cref="HttpFileUploadClient"/>) uses an injected <see cref="HttpClient"/>
/// whose authentication handler is configured by the host application.
/// </summary>
public interface IFileUploadClient
{
    /// <summary>
    /// Uploads a file stream to the server and returns the resulting metadata record.
    /// </summary>
    /// <param name="content">The file byte stream. Callers may pass a browser file stream directly.</param>
    /// <param name="fileName">Original file name without extension, stored for display purposes.</param>
    /// <param name="contentType">MIME content type of the file.</param>
    /// <param name="ownerScopeKey">
    /// Optional opaque scope key (e.g. a tenant identifier) forwarded to the server for
    /// access-scoping. The library never interprets this value.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored file metadata, including the assigned <see cref="FileUploadResult.Id"/>.</returns>
    Task<FileUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string? ownerScopeKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sends a DELETE request to remove the file record and its stored content from the server.
    /// </summary>
    /// <param name="id">Identifier of the file to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Obtains a short-lived signed access token for the given file from the server's
    /// <c>GET {id}/token</c> endpoint. The caller appends the returned token to download
    /// and thumbnail URLs via <see cref="IBaseFile.GetDownloadUrl(string, string?)"/> and
    /// <see cref="IBaseFile.GetThumbnailUrl(string, string?)"/> so that protected
    /// <c>&lt;img&gt;</c> and <c>&lt;a&gt;</c> elements can bypass bearer-auth.
    /// </summary>
    /// <param name="id">Identifier of the file whose token is requested.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The signed token string issued by the server. Returns an empty string when the server
    /// returns a null or empty token body.
    /// </returns>
    Task<string> GetAccessTokenAsync(Guid id, CancellationToken cancellationToken);
}
