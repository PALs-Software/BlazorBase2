using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Help;

/// <summary>
/// Reads the help content as static files over HTTP, from <c>{ContentBasePath}/{culture}/{path}</c> below the
/// application's base address. Works the same in Blazor Server and WebAssembly; every file is fetched once.
/// </summary>
/// <remarks>
/// A host whose unknown URLs fall back to <c>index.html</c> would answer a missing file with a page; HTML
/// responses are therefore treated as "not found".
/// </remarks>
public sealed class HelpContentSource(HttpClient httpClient, NavigationManager navigationManager, IOptions<HelpOptions> options) : IHelpContentSource
{
    #region Injects

    private readonly HttpClient HttpClient = httpClient;
    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IOptions<HelpOptions> Options = options;

    #endregion

    #region Members

    private readonly ConcurrentDictionary<string, Task<string?>> Cache = new(StringComparer.Ordinal);

    #endregion

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
    public Task<string?> GetFileAsync(string cultureFolder, string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cultureFolder) || !HelpPaths.IsSafe(path) || !HelpPaths.IsSafe(cultureFolder))
            return Task.FromResult<string?>(null);

        var key = cultureFolder + "|" + path;
        var fetch = Cache.GetOrAdd(key, _ => FetchAsync(cultureFolder, path, key));

        return fetch.WaitAsync(cancellationToken);
    }

    private async Task<string?> FetchAsync(string cultureFolder, string path, string key)
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

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType == "text/html")
                return null;

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Cache.TryRemove(key, out _);
            return null;
        }
    }
}
