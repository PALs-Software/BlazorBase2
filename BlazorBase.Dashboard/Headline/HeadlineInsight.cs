using Microsoft.AspNetCore.Components;

namespace BlazorBase.Dashboard;

/// <summary>
/// One line of news under the greeting of a <c>HeadlinePart</c>.
/// </summary>
public sealed class HeadlineInsight
{
    /// <summary>
    /// Creates an insight from text; segments wrapped in <c>**double asterisks**</c> are shown bold. The text is never
    /// interpreted as HTML.
    /// </summary>
    /// <param name="priority">Sort key across all providers; lower values come first.</param>
    /// <param name="text">The sentence, for example <c>**3 budget boosts** are waiting for your decision.</c></param>
    public HeadlineInsight(int priority, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Priority = priority;
        Text = text;
    }

    /// <summary>
    /// Creates an insight from markup, for a sentence that contains a link.
    /// </summary>
    /// <param name="priority">Sort key across all providers; lower values come first.</param>
    /// <param name="content">The rendered sentence.</param>
    public HeadlineInsight(int priority, RenderFragment content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Priority = priority;
        Content = content;
    }

    /// <summary>
    /// Sort key across all providers; lower values come first.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// The sentence with <c>**bold**</c> markers; null when the insight was built from markup.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// The sentence as markup; null when the insight was built from text.
    /// </summary>
    public RenderFragment? Content { get; }
}
