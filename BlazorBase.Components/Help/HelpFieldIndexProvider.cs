using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Help;

/// <summary>
/// Resolves the topic of a page with the <see cref="IHelpTopicResolver"/>, reads it from the <see cref="IHelpContentSource"/>
/// (which caches the files) and turns its field entries into <see cref="HelpFieldTip"/>s. The result is kept per
/// topic and culture for the lifetime of the scope; a topic that could not be read is not remembered, so it is asked for
/// again once the user is signed in.
/// </summary>
public sealed class HelpFieldIndexProvider(
    IHelpContentSource contentSource,
    IHelpTopicResolver topicResolver,
    IOptions<HelpOptions> options) : IHelpFieldIndex
{
    #region Injects

    private readonly IHelpContentSource ContentSource = contentSource;
    private readonly IHelpTopicResolver TopicResolver = topicResolver;
    private readonly IOptions<HelpOptions> Options = options;

    #endregion

    #region Members

    private readonly ConcurrentDictionary<string, HelpFieldIndexData> Cache = new(StringComparer.Ordinal);

    #endregion

    /// <inheritdoc/>
    public async Task<HelpFieldIndexData?> GetForPathAsync(string? path, CancellationToken cancellationToken = default)
    {
        var topic = TopicResolver.ResolveTopic(path);

        if (string.IsNullOrWhiteSpace(topic))
            return null;

        var culture = CultureInfo.CurrentUICulture.Name;
        var cacheKey = topic + "|" + culture;

        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var content = await ContentSource.GetTopicAsync(topic, culture, cancellationToken);

        if (content is null || !string.Equals(content.Topic, topic, StringComparison.OrdinalIgnoreCase))
            return null;

        var data = await BuildAsync(content, cancellationToken);
        Cache[cacheKey] = data;

        return data;
    }

    private async Task<HelpFieldIndexData> BuildAsync(HelpSourceDocument content, CancellationToken cancellationToken)
    {
        var settings = Options.Value;
        var context = new HelpProcessingContext
        {
            Topic = content.Topic,
            ContentRootUrl = settings.ContentBasePath.Trim('/') + "/" + content.Culture + "/",
            LoadFileAsync = (path, token) => ContentSource.GetFileAsync(content.Culture, path, token),
            ResolveRoute = TopicResolver.GetRoute,
            Culture = content.Culture,
        };
        var document = await HelpMarkdownProcessor.ProcessAsync(content.Markdown, context, cancellationToken);
        var structure = HelpStructureReader.Read(document);

        return new HelpFieldIndexData(content.Topic, CreateTips(structure.Fields));
    }

    /// <summary>Returns one tip per label from <paramref name="fields"/>: entries without text are skipped and, for a label that occurs twice, the first entry wins.</summary>
    public static IReadOnlyList<HelpFieldTip> CreateTips(IEnumerable<HelpFieldEntry> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var seen = new HashSet<string>(StringComparer.Ordinal);

        return fields
            .Where(field => field.ShortHtml.Length > 0 && seen.Add(field.Key))
            .Select(field => new HelpFieldTip(field.Key, field.Label, field.ShortHtml, field.Id))
            .ToList();
    }
}
