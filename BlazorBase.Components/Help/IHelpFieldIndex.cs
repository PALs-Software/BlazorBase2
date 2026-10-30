namespace BlazorBase.Components.Help;

/// <summary>Builds the index of field tooltips of the help topic that belongs to a page.</summary>
public interface IHelpFieldIndex
{
    /// <summary>
    /// Returns the tips of the topic of the page at <paramref name="path"/>: the entries below the
    /// <c>&lt;!-- help:fields --&gt;</c> marker of its help content. Null when the page has no topic, when the topic has no
    /// content of its own (the fallback topic is not used), or when the content cannot be read — for example because the
    /// user is not signed in.
    /// </summary>
    /// <param name="path">The path relative to the application's base address, as <see cref="IHelpTopicResolver.ResolveTopic"/> takes it.</param>
    /// <param name="cancellationToken">Cancels the wait for the content.</param>
    Task<HelpFieldIndexData?> GetForPathAsync(string? path, CancellationToken cancellationToken = default);
}
