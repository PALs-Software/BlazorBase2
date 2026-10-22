using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Help;

/// <summary>
/// Reads the help content over HTTP, from <c>{ContentBasePath}/{culture}/{path}</c> below the application's base
/// address. Works the same in Blazor Server and WebAssembly; every file is fetched once.
/// </summary>
/// <remarks>
/// The request is same-origin, so the browser sends the user's cookies — that is what lets the host serve the
/// content to signed-in users only (<c>MapBlazorBaseHelpContent</c> of <c>BlazorBase.Components.Server</c>). An
/// answer that is not a success — <c>404</c> for a missing file, <c>401</c> or <c>403</c> when the user is not
/// signed in or not allowed — means "no help"; a <c>401</c> or <c>403</c> is not remembered, so the file is asked for
/// again once the user is signed in. A host whose unknown URLs fall back to <c>index.html</c> would answer a missing
/// file with a page; HTML responses are therefore treated as "not found" as well.
/// </remarks>
public sealed class HelpContentSource(HttpClient httpClient, NavigationManager navigationManager, IOptions<HelpOptions> options) : IHelpContentSource
{
    #region Injects

    private readonly HttpClient HttpClient = httpClient;
    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IOptions<HelpOptions> Options = options;

    #endregion

    #region Members

    private readonly ConcurrentDictionary<string, Task<FetchResult>> Cache = new(StringComparer.Ordinal);

    #endregion

    private sealed record FetchResult(string? Content, bool IsFinal);

    /// <inheritdoc/>
    public async Task<HelpSourceDocument?> GetTopicAsync(string topic, string culture, CancellationToken cancellationToken = default)
    {
        var settings = Options.Value;
        var folders = new[] { settings.ResolveCultureFolder(culture), settings.DefaultCulture }
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var topics = new[] { topic, settings.FallbackTopic }
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in topics)
            foreach (var folder in folders)
                if (await GetFileAsync(folder, candidate + ".md", cancellationToken) is { } markdown)
                    return new HelpSourceDocument(candidate, folder, markdown);

        return null;
    }

    /// <inheritdoc/>
    public async Task<string?> GetFileAsync(string cultureFolder, string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cultureFolder) || !HelpPaths.IsSafe(path) || !HelpPaths.IsSafe(cultureFolder))
            return null;

        var key = cultureFolder + "|" + path;
        var fetch = Cache.GetOrAdd(key, _ => FetchAsync(cultureFolder, path));
        var result = await fetch.WaitAsync(cancellationToken);

        if (!result.IsFinal)
            Cache.TryRemove(new KeyValuePair<string, Task<FetchResult>>(key, fetch));

        return result.Content;
    }

    private async Task<FetchResult> FetchAsync(string cultureFolder, string path)
    {
        var relative = string.Join('/', Options.Value.ContentBasePath.Split('/')
            .Append(cultureFolder)
            .Concat(path.Split('/'))
            .Where(part => part.Length > 0)
            .Select(Uri.EscapeDataString));

        try
        {
            await Task.Yield();
            using var response = await HttpClient.GetAsync(new Uri(new Uri(NavigationManager.BaseUri), relative));

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new FetchResult(null, IsFinal: false);

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType == "text/html")
                return new FetchResult(null, IsFinal: true);

            return new FetchResult(await response.Content.ReadAsStringAsync(), IsFinal: true);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new FetchResult(null, IsFinal: false);
        }
    }
}
