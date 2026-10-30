namespace BlazorBase.Components.Help;

/// <summary>Reads the Markdown files of the help content.</summary>
public interface IHelpContentSource
{
    /// <summary>
    /// Returns the content of <paramref name="topic"/> in <paramref name="culture"/>; when it does not exist, in
    /// the default culture; when that does not exist either, the same for the fallback topic. Null when
    /// nothing exists.
    /// </summary>
    /// <param name="topic">The topic, e.g. <c>general/blog</c>.</param>
    /// <param name="culture">The UI culture name, e.g. <c>de</c> or <c>de-DE</c>.</param>
    /// <param name="cancellationToken">Cancels the wait for the content.</param>
    Task<HelpSourceDocument?> GetTopicAsync(string topic, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one file of a culture folder, e.g. <c>includes/list-basics.md</c>, without any fallback; null when
    /// it does not exist.
    /// </summary>
    /// <param name="cultureFolder">The culture folder, e.g. <c>en-US</c>.</param>
    /// <param name="path">The path below the culture folder, using <c>/</c> as the separator.</param>
    /// <param name="cancellationToken">Cancels the wait for the content.</param>
    Task<string?> GetFileAsync(string cultureFolder, string path, CancellationToken cancellationToken = default);
}
