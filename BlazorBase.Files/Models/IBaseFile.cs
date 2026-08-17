namespace BlazorBase.Files.Models;

/// <summary>
/// Metadata contract for a stored file. Provides identity, classification, and URL-construction
/// helpers so components can render download and thumbnail links without coupling to server routing.
/// </summary>
public interface IBaseFile
{
    /// <summary>The stable persistent identifier assigned at upload time.</summary>
    Guid Id { get; set; }

    /// <summary>Original file name without extension (user-supplied, stored for display only).</summary>
    string FileName { get; set; }

    /// <summary>File extension including the leading dot (e.g. <c>.jpg</c>).</summary>
    string Extension { get; set; }

    /// <summary>MIME content type (e.g. <c>image/jpeg</c>).</summary>
    string ContentType { get; set; }

    /// <summary>File size in bytes.</summary>
    long FileSize { get; set; }

    /// <summary>SHA-256 hex digest of the file content, used as a cache-bust query parameter.</summary>
    string? Hash { get; set; }

    /// <summary>
    /// Optional opaque scope key set by the host (e.g. a tenant or organization identifier) to
    /// partition file access. The library never interprets this value.
    /// </summary>
    string? OwnerScopeKey { get; set; }

    /// <summary>
    /// Returns <see langword="true"/> when the file's content type indicates an image
    /// (<c>image/*</c>).
    /// </summary>
    bool IsImage();

    /// <summary>
    /// Builds the URL for downloading the full file. The <paramref name="route"/> is the base
    /// path of the server's file controller (e.g. <c>api/files</c>). An optional
    /// <c>?h={Hash}</c> query parameter is appended when a hash is available so browsers
    /// re-fetch the file when its content changes.
    /// </summary>
    string GetDownloadUrl(string route);

    /// <summary>
    /// Builds the token-bearing URL for downloading the full file. Appends the
    /// <paramref name="token"/> as a <c>token=…</c> query parameter (URL-encoded) using the
    /// correct separator: <c>&amp;</c> when a hash parameter is already present, <c>?</c>
    /// otherwise. When <paramref name="token"/> is <see langword="null"/> or empty the method
    /// returns the same URL as <see cref="GetDownloadUrl(string)"/>.
    /// </summary>
    /// <param name="route">Base path of the server's file controller (e.g. <c>api/files</c>).</param>
    /// <param name="token">Short-lived signed access token obtained from <c>GET {id}/token</c>.</param>
    string GetDownloadUrl(string route, string? token);

    /// <summary>
    /// Builds the URL for the image thumbnail. Equivalent to <see cref="GetDownloadUrl(string)"/>
    /// with a <c>/thumbnail</c> suffix. For non-image files the caller should fall back to a
    /// placeholder; this method does not validate file type.
    /// </summary>
    string GetThumbnailUrl(string route);

    /// <summary>
    /// Builds the token-bearing URL for the image thumbnail. Appends the
    /// <paramref name="token"/> as a <c>token=…</c> query parameter (URL-encoded) using the
    /// correct separator: <c>&amp;</c> when a hash parameter is already present, <c>?</c>
    /// otherwise. When <paramref name="token"/> is <see langword="null"/> or empty the method
    /// returns the same URL as <see cref="GetThumbnailUrl(string)"/>.
    /// </summary>
    /// <param name="route">Base path of the server's file controller (e.g. <c>api/files</c>).</param>
    /// <param name="token">Short-lived signed access token obtained from <c>GET {id}/token</c>.</param>
    string GetThumbnailUrl(string route, string? token);
}
