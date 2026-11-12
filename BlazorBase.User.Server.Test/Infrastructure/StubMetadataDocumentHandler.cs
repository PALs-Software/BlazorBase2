using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// Stands in for the internet when the server fetches client id metadata documents: answers each URL
/// with the response a test configured and counts how often every URL was requested.
/// </summary>
public sealed class StubMetadataDocumentHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, Func<HttpResponseMessage>> Responses = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> RequestCounts = new(StringComparer.Ordinal);

    /// <summary>The <c>Accept</c> header of the last request.</summary>
    public string? LastAcceptHeader { get; private set; }

    /// <summary>Serves <paramref name="document"/> as JSON at <paramref name="url"/>.</summary>
    public void Serve(string url, object document, string? cacheControl = null, string mediaType = "application/json") =>
        Respond(url, () => CreateResponse(HttpStatusCode.OK, JsonSerializer.Serialize(document), mediaType, cacheControl));

    /// <summary>Answers <paramref name="url"/> with <paramref name="statusCode"/> and an empty body.</summary>
    public void Fail(string url, HttpStatusCode statusCode) =>
        Respond(url, () => CreateResponse(statusCode, string.Empty, "text/plain", null));

    /// <summary>Lets every request to <paramref name="url"/> fail at the network level.</summary>
    public void Unreachable(string url) =>
        Respond(url, () => throw new HttpRequestException("Connection refused."));

    public void Respond(string url, Func<HttpResponseMessage> response) => Responses[url] = response;

    public int RequestCount(string url) => RequestCounts.GetValueOrDefault(url);

    /// <summary>The standard document of a public client at <paramref name="clientId"/>.</summary>
    public static Dictionary<string, object> CreateDocument(string clientId, string[]? redirectUris = null, string clientName = "Claude") => new()
    {
        ["client_id"] = clientId,
        ["client_name"] = clientName,
        ["redirect_uris"] = redirectUris ?? [OAuthTestHost.RedirectUri],
        ["grant_types"] = new[] { "authorization_code", "refresh_token" },
        ["response_types"] = new[] { "code" },
        ["token_endpoint_auth_method"] = "none",
    };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.OriginalString;
        RequestCounts.AddOrUpdate(url, 1, (_, count) => count + 1);
        LastAcceptHeader = request.Headers.Accept.ToString();

        if (!Responses.TryGetValue(url, out var response))
            return Task.FromResult(CreateResponse(HttpStatusCode.NotFound, string.Empty, "text/plain", null));

        return Task.FromResult(response());
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string body, string mediaType, string? cacheControl)
    {
        var response = new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

        if (cacheControl is not null)
            response.Headers.CacheControl = CacheControlHeaderValue.Parse(cacheControl);

        return response;
    }
}
