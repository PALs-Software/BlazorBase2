namespace BlazorBase.Components.Help;

/// <summary>The field tooltips of one help topic.</summary>
/// <param name="Topic">The topic the tips come from, e.g. <c>general/blog</c>.</param>
/// <param name="Tips">One tip per label; when a label occurs twice in the topic the first entry wins.</param>
public sealed record HelpFieldIndexData(string Topic, IReadOnlyList<HelpFieldTip> Tips);
