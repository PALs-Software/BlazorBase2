namespace BlazorBase.Components.Help;

/// <summary>
/// Marks a routable page with the help topic the help panel shows for it, e.g. <c>[HelpTopic("general/blog")]</c>.
/// </summary>
/// <remarks>
/// The topic is the path of a Markdown file below the culture folder of the help content, without the
/// <c>.md</c> extension: <c>general/blog</c> is read from <c>help/en-US/general/blog.md</c>. By convention a page topic
/// is <c>&lt;category&gt;/&lt;page&gt;</c>, the category being the folder of the sidebar group the page is in. Pages
/// without the attribute show <see cref="HelpOptions.FallbackTopic"/>. Topics that no page carries (for example
/// <c>concepts/budgets-and-boosts</c>) are reached through links in the Markdown and open inside the panel.
/// </remarks>
/// <param name="topic">The topic, using <c>/</c> as the separator.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HelpTopicAttribute(string topic) : Attribute
{
    /// <summary>The topic, e.g. <c>general/blog</c>.</summary>
    public string Topic { get; } = topic;
}
