namespace BlazorBase.Components.Help;

/// <summary>
/// Marks a routable page with the help topic the help panel shows for it, e.g. <c>[HelpTopic("pages/blog")]</c>.
/// </summary>
/// <remarks>
/// The topic is the path of a Markdown file below the culture folder of the help content, without the
/// <c>.md</c> extension: <c>pages/blog</c> is read from <c>help/en-US/pages/blog.md</c>. Pages without the
/// attribute show <see cref="HelpOptions.FallbackTopic"/>.
/// </remarks>
/// <param name="topic">The topic, using <c>/</c> as the separator.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HelpTopicAttribute(string topic) : Attribute
{
    /// <summary>The topic, e.g. <c>pages/blog</c>.</summary>
    public string Topic { get; } = topic;
}
