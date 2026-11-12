using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BlazorBase.User.Server.OAuth.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// Fetches client id metadata documents through the named <see cref="HttpClientName"/> client, whose
/// primary handler (<see cref="OAuthClientIdMetadataDocumentHttpHandler"/>) refuses special-use
/// addresses, redirects, proxies and cookies. Only a <c>200</c> JSON response of at most
/// <see cref="MaximumDocumentBytes"/> counts.
/// </summary>
/// <remarks>
/// Requests are pinned to HTTP/1.1 or lower: HTTP/3 runs over QUIC, which does not go through the
/// handler's connect callback and would therefore skip the address check. Failure messages returned to
/// the caller are deliberately generic - the detail goes to the log only - so the endpoint cannot be used
/// to learn how internal host names resolve.
/// </remarks>
public sealed class OAuthClientIdMetadataDocumentFetcher(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<OAuthServerOptions> options,
    ILogger<OAuthClientIdMetadataDocumentFetcher> logger) : IOAuthClientIdMetadataDocumentFetcher
{
    #region Injects
    private readonly IHttpClientFactory HttpClientFactory = httpClientFactory;
    private readonly IOptionsMonitor<OAuthServerOptions> Options = options;
    private readonly ILogger<OAuthClientIdMetadataDocumentFetcher> Logger = logger;
    #endregion

    private const string UnreachableMessage = "The client id metadata document could not be fetched.";
    private const string UserAgentProduct = "BlazorBase-OAuth-ClientMetadataFetcher";
    private const string UserAgentVersion = "1.0";

    /// <summary>The name of the <see cref="HttpClient"/> the documents are fetched with.</summary>
    public const string HttpClientName = "BlazorBase.OAuth.ClientIdMetadataDocuments";

    /// <summary>The most bytes read from one document, as the draft recommends (section 10.11).</summary>
    public const int MaximumDocumentBytes = 5 * 1024;

    /// <inheritdoc/>
    public async Task<OAuthClientIdMetadataDocumentFetchResult> FetchAsync(string clientIdUrl, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Options.CurrentValue.ClientIdMetadataDocumentFetchTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, clientIdUrl)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(UserAgentProduct, UserAgentVersion));

        try
        {
            using var response = await HttpClientFactory.CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            return await ReadResponseAsync(response, timeout.Token);
        }
        catch (HttpRequestException exception) when (FindForbiddenAddress(exception) is { } forbiddenAddress)
        {
            Logger.LogWarning("Refused to fetch the client id metadata document at {ClientId}: {Reason}", clientIdUrl, forbiddenAddress.Message);
            return OAuthClientIdMetadataDocumentFetchResult.Terminal(UnreachableMessage);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            Logger.LogWarning(exception, "Fetching the client id metadata document at {ClientId} failed.", clientIdUrl);
            return OAuthClientIdMetadataDocumentFetchResult.Transient(UnreachableMessage);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OAuthClientIdMetadataDocumentFetchResult.Transient("Fetching the client id metadata document timed out.");
        }
    }

    private static async Task<OAuthClientIdMetadataDocumentFetchResult> ReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.OK)
            return IsTransientStatus(response.StatusCode)
                ? OAuthClientIdMetadataDocumentFetchResult.Transient($"The client id metadata document host answered {(int)response.StatusCode}.")
                : OAuthClientIdMetadataDocumentFetchResult.Terminal($"The client id metadata document host answered {(int)response.StatusCode}; only 200 is accepted and redirects are not followed.");

        if (!IsJsonMediaType(response.Content.Headers.ContentType?.MediaType))
            return OAuthClientIdMetadataDocumentFetchResult.Terminal("The client id metadata document is not served as JSON.");

        if (response.Content.Headers.ContentLength > MaximumDocumentBytes)
            return OAuthClientIdMetadataDocumentFetchResult.Terminal($"The client id metadata document exceeds {MaximumDocumentBytes} bytes.");

        var content = await ReadLimitedAsync(response.Content, cancellationToken);

        if (content is null)
            return OAuthClientIdMetadataDocumentFetchResult.Terminal($"The client id metadata document exceeds {MaximumDocumentBytes} bytes.");

        OAuthClientIdMetadataDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<OAuthClientIdMetadataDocument>(content);
        }
        catch (JsonException)
        {
            return OAuthClientIdMetadataDocumentFetchResult.Terminal("The client id metadata document is not valid client metadata JSON.");
        }

        if (document is null)
            return OAuthClientIdMetadataDocumentFetchResult.Terminal("The client id metadata document is empty.");

        return OAuthClientIdMetadataDocumentFetchResult.Success(document, ReadCacheDuration(response.Headers.CacheControl));
    }

    /// <summary>Reads at most <see cref="MaximumDocumentBytes"/>; returns <see langword="null"/> when the body is longer.</summary>
    private static async Task<byte[]?> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaximumDocumentBytes + 1];
        var length = 0;

        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);

            if (read == 0)
                break;

            length += read;
        }

        if (length > MaximumDocumentBytes)
            return null;

        return buffer[..length];
    }

    /// <summary>
    /// <c>no-store</c> and <c>no-cache</c> mean "do not reuse", <c>max-age</c> its value; anything else
    /// states nothing, and the caller applies its minimum. <c>Expires</c> is ignored, as the draft allows.
    /// </summary>
    private static TimeSpan? ReadCacheDuration(CacheControlHeaderValue? cacheControl)
    {
        if (cacheControl is null)
            return null;

        if (cacheControl.NoStore || cacheControl.NoCache)
            return TimeSpan.Zero;

        return cacheControl.MaxAge;
    }

    private static bool IsJsonMediaType(string? mediaType) =>
        mediaType is not null &&
        (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

    private static bool IsTransientStatus(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static OAuthForbiddenAddressException? FindForbiddenAddress(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is OAuthForbiddenAddressException forbiddenAddress)
                return forbiddenAddress;

        return null;
    }
}
