namespace BlazorBase.Dashboard;

/// <summary>
/// Splits the <c>**bold**</c> markers of a headline insight text into plain and bold segments.
/// </summary>
public static class HeadlineText
{
    /// <summary>
    /// A piece of an insight text.
    /// </summary>
    /// <param name="Text">The characters of the piece, without markers.</param>
    /// <param name="IsBold">True for a piece that was wrapped in double asterisks.</param>
    public readonly record struct Segment(string Text, bool IsBold);

    /// <summary>
    /// Splits <paramref name="text"/> at the double-asterisk markers; the odd pieces are bold. A marker without a partner
    /// stays literal.
    /// </summary>
    public static IReadOnlyList<Segment> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var parts = text.Split("**");

        if (parts.Length == 1)
            return [new Segment(text, false)];

        var lastBold = 2 * ((parts.Length - 1) / 2) - 1;
        var segments = new List<Segment>(parts.Length);

        for (var index = 0; index < parts.Length; index++)
        {
            var isBold = index % 2 == 1 && index <= lastBold;

            if (index % 2 == 1 && index > lastBold)
                segments.Add(new Segment("**" + parts[index], false));
            else if (parts[index].Length > 0)
                segments.Add(new Segment(parts[index], isBold));
        }

        return segments;
    }
}
