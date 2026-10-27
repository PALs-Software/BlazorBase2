using System.Text;

namespace BlazorBase.Components.CommandPalette;

internal enum MatchKind
{
    None,
    Contains,
    WordStart,
    Starts,
}

internal readonly record struct TextSegment(string Text, bool IsMatch);

/// <summary>
/// Case and diacritic insensitive text matching for the command palette. Folding maps every UTF-16 character to
/// exactly one character, so positions found in the folded text are valid in the original text.
/// </summary>
internal static class CommandPaletteText
{
    public static string Fold(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
                span[index] = FoldChar(source[index]);
        });
    }

    public static IReadOnlyList<string> Tokenize(string? query)
    {
        var folded = Fold(query);
        var tokens = new List<string>();
        var current = new StringBuilder();

        foreach (var character in folded)
        {
            if (char.IsWhiteSpace(character))
            {
                Flush(current, tokens);
                continue;
            }

            current.Append(character);
        }

        Flush(current, tokens);
        return tokens;
    }

    public static MatchKind Match(string foldedText, string token, out int index)
    {
        index = -1;
        if (token.Length == 0 || foldedText.Length < token.Length)
            return MatchKind.None;

        if (foldedText.StartsWith(token, StringComparison.Ordinal))
        {
            index = 0;
            return MatchKind.Starts;
        }

        var firstOccurrence = -1;
        var position = foldedText.IndexOf(token, StringComparison.Ordinal);
        while (position >= 0)
        {
            if (firstOccurrence < 0)
                firstOccurrence = position;

            if (!char.IsLetterOrDigit(foldedText[position - 1]))
            {
                index = position;
                return MatchKind.WordStart;
            }

            position = position + 1 < foldedText.Length
                ? foldedText.IndexOf(token, position + 1, StringComparison.Ordinal)
                : -1;
        }

        if (firstOccurrence < 0)
            return MatchKind.None;

        index = firstOccurrence;
        return MatchKind.Contains;
    }

    /// <summary>Splits <paramref name="text"/> into plain and matching segments for all tokens; the text is never altered.</summary>
    public static IReadOnlyList<TextSegment> Highlight(string text, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0 || text.Length == 0)
            return [new TextSegment(text, false)];

        var folded = Fold(text);
        var ranges = new List<(int Start, int End)>();
        foreach (var token in tokens)
        {
            if (Match(folded, token, out var index) != MatchKind.None)
                ranges.Add((index, index + token.Length));
        }

        if (ranges.Count == 0)
            return [new TextSegment(text, false)];

        ranges.Sort();
        var merged = new List<(int Start, int End)> { ranges[0] };
        foreach (var range in ranges.Skip(1))
        {
            var last = merged[^1];
            if (range.Start <= last.End)
                merged[^1] = (last.Start, Math.Max(last.End, range.End));
            else
                merged.Add(range);
        }

        var segments = new List<TextSegment>();
        var cursor = 0;
        foreach (var (start, end) in merged)
        {
            if (start > cursor)
                segments.Add(new TextSegment(text[cursor..start], false));

            segments.Add(new TextSegment(text[start..end], true));
            cursor = end;
        }

        if (cursor < text.Length)
            segments.Add(new TextSegment(text[cursor..], false));

        return segments;
    }

    private static void Flush(StringBuilder current, List<string> tokens)
    {
        if (current.Length == 0)
            return;

        var token = current.ToString();
        current.Clear();
        if (!tokens.Contains(token))
            tokens.Add(token);
    }

    private static char FoldChar(char character)
    {
        var lower = char.ToLowerInvariant(character);
        if (lower < 128)
            return lower;

        var decomposed = lower.ToString().Normalize(NormalizationForm.FormD);
        return decomposed.Length > 0 ? decomposed[0] : lower;
    }
}
