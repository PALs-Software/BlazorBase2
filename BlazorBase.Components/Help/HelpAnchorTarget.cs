namespace BlazorBase.Components.Help;

/// <summary>A place in a help topic: the topic and the id of the heading or field entry in it.</summary>
/// <param name="Topic">The topic, e.g. <c>general/blog</c>.</param>
/// <param name="Anchor">The id of the element in the rendered topic, e.g. <c>field-monthly-budget</c>.</param>
public sealed record HelpAnchorTarget(string Topic, string Anchor);
