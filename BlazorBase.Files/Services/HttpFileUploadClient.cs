using System.Net.Http.Json;
using BlazorBase.Files.Models;

namespace BlazorBase.Files.Services;

/// <summary>
/// HTTP implementation of <see cref="IFileUploadClient"/>. Sends multipart POST requests for
/// uploads and DELETE requests for removals. The injected <see cref="HttpClient"/> must be
/// pre-configured by the host with the correct base address and authentication handler
/// (typically <c>AuthTokenHandler</c>); this class contains no auth logic.
/// </summary>
public class HttpFileUploadClient(HttpClient httpClient) : IFileUploadClient
{
    #region Injects
    private readonly HttpClient HttpClient = httpClient;
    #endregion

    /// <inheritdoc/>
    public async Task<FileUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string? ownerScopeKey,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "file", fileName);

        if (ownerScopeKey is not null)
            form.Add(new StringContent(ownerScopeKey), "ownerScopeKey");

        var response = await HttpClient.PostAsync(string.Empty, form, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FileUploadResult>(cancellationToken).ConfigureAwait(false);
        return result!;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await HttpClient.DeleteAsync(id.ToString(), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task<string> GetAccessTokenAsync(Guid id, CancellationToken cancellationToken)
    {
        var token = await HttpClient.GetFromJsonAsync<string>($"{id}/token", cancellationToken).ConfigureAwait(false);
        return token ?? string.Empty;
    }
}
