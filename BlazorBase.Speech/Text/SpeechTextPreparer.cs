using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Speech.Text;

/// <summary>
/// Turns chat-style Markdown into plain-text segments a text-to-speech engine can read aloud.
/// </summary>
/// <remarks>
/// Markup is removed rather than spoken: emphasis, headings, list markers and link targets disappear,
/// link texts and inline code stay. Fenced code blocks and tables cannot be read aloud sensibly, so each
/// one is replaced by a short localized hint pointing to the written text. The result is split into
/// sentences and grouped into segments: the first sentence travels alone so playback can start while
/// the rest is still being synthesized, later sentences are combined up to
/// <see cref="MaximumSegmentLength"/> characters to keep the number of requests low.
/// </remarks>
public partial class SpeechTextPreparer(IStringLocalizer<SpeechTextPreparer> localizer)
{
    #region Injects
    private readonly IStringLocalizer<SpeechTextPreparer> Localizer = localizer;
    #endregion

    /// <summary>Upper bound for one segment; longer sentences are split at a comma or a space.</summary>
    public const int MaximumSegmentLength = 300;

    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "bzw.", "ca.", "etc.", "usw.", "evtl.", "ggf.", "inkl.", "exkl.", "vgl.", "bspw.", "sog.", "zzgl.",
        "dr.", "prof.", "nr.", "st.", "min.", "max.", "mio.", "mrd.", "vs.", "mr.", "mrs.", "ms.", "approx.", "no."
    };

    private static readonly char[] TerminalPunctuation = ['.', '!', '?', '…', ':', ';'];

    /// <summary>Converts Markdown into speakable segments, in reading order.</summary>
    /// <param name="markdown">Text as written in the chat; Markdown is optional.</param>
    /// <param name="language">
    /// Two-letter code of the language the text is read in. The hints for code blocks, tables and links are
    /// spoken in it, so a German answer does not get an English hint in the middle; the UI culture when
    /// <see langword="null"/> or unknown.
    /// </param>
    /// <returns>The segments to synthesize one after another; empty when nothing speakable is left.</returns>
    public IReadOnlyList<string> Prepare(string? markdown, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var hints = ResolveHints(language);
        var sentences = ToPlainTextLines(markdown, hints)
            .SelectMany(SplitIntoSentences)
            .Where(ContainsLetterOrDigit)
            .Select(EnsureTerminalPunctuation)
            .SelectMany(SplitOverlongSentence)
            .ToList();

        return GroupIntoSegments(sentences);
    }

    private SpeechHints ResolveHints(string? language)
    {
        var culture = TryGetCulture(language);

        if (culture is null)
            return ReadHints();

        var previousCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = culture;
            return ReadHints();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    private SpeechHints ReadHints() => new(Localizer["CodeBlock"], Localizer["Table"], Localizer["Link"]);

    private static CultureInfo? TryGetCulture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private IEnumerable<string> ToPlainTextLines(string markdown, SpeechHints hints)
    {
        var codeBlockHint = hints.CodeBlock;
        var tableHint = hints.Table;
        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var withoutCodeBlocks = FencedCodeBlock().Replace(normalized, _ => $"\n{codeBlockHint}\n");
        string? previousLine = null;

        foreach (var line in withoutCodeBlocks.Split('\n'))
        {
            var plainLine = TableRow().IsMatch(line) ? tableHint : ToPlainLine(line, hints.Link);

            if (plainLine.Length == 0)
                continue;

            if (IsRepeatedHint(plainLine, previousLine, codeBlockHint, tableHint))
                continue;

            previousLine = plainLine;
            yield return plainLine;
        }
    }

    private static bool IsRepeatedHint(string line, string? previousLine, string codeBlockHint, string tableHint)
        => (line == codeBlockHint || line == tableHint) && line == previousLine;

    private static string ToPlainLine(string line, string linkWord)
    {
        if (HorizontalRule().IsMatch(line))
            return string.Empty;

        var text = Heading().Replace(line, string.Empty);
        text = Blockquote().Replace(text, string.Empty);
        text = ListMarker().Replace(text, string.Empty);
        text = TaskCheckbox().Replace(text, string.Empty);
        text = Image().Replace(text, "$1");
        text = Link().Replace(text, "$1");
        text = AutoLink().Replace(text, linkWord);
        text = InlineCode().Replace(text, "$1");
        text = HtmlTag().Replace(text, string.Empty);
        text = StrongEmphasis().Replace(text, "$2");
        text = StarEmphasis().Replace(text, "$1");
        text = UnderscoreEmphasis().Replace(text, "$1");
        text = Strikethrough().Replace(text, "$1");
        text = BareUrl().Replace(text, linkWord);
        text = TrailingHeadingHashes().Replace(text, string.Empty);

        return Whitespace().Replace(text, " ").Trim();
    }

    private static IEnumerable<string> SplitIntoSentences(string line)
    {
        var start = 0;

        foreach (Match boundary in SentenceBoundary().Matches(line))
        {
            var candidate = line[start..boundary.Index].Trim();

            if (EndsWithAbbreviation(candidate))
                continue;

            if (candidate.Length > 0)
                yield return candidate;

            start = boundary.Index + boundary.Length;
        }

        var rest = line[start..].Trim();

        if (rest.Length > 0)
            yield return rest;
    }

    private static bool EndsWithAbbreviation(string candidate)
    {
        if (!candidate.EndsWith('.'))
            return false;

        var lastSpace = candidate.LastIndexOf(' ');
        var lastWord = lastSpace < 0 ? candidate : candidate[(lastSpace + 1)..];
        var withoutFinalDot = lastWord.TrimEnd('.');

        if (withoutFinalDot.Length == 0)
            return false;

        if (Abbreviations.Contains(lastWord))
            return true;

        if (withoutFinalDot.Length == 1 && char.IsLetter(withoutFinalDot[0]))
            return true;

        if (withoutFinalDot.All(char.IsDigit))
            return true;

        return withoutFinalDot.Contains('.');
    }

    private static bool ContainsLetterOrDigit(string sentence) => sentence.Any(char.IsLetterOrDigit);

    private static string EnsureTerminalPunctuation(string sentence)
    {
        var withoutClosingMarks = sentence.TrimEnd('"', '\'', '»', '«', '“', '”', ')', ']');

        if (withoutClosingMarks.Length > 0 && TerminalPunctuation.Contains(withoutClosingMarks[^1]))
            return sentence;

        return sentence + ".";
    }

    private static IEnumerable<string> SplitOverlongSentence(string sentence)
    {
        var remaining = sentence;

        while (remaining.Length > MaximumSegmentLength)
        {
            var window = remaining[..MaximumSegmentLength];
            var cut = LastBreakOpportunity(window);
            yield return remaining[..cut].Trim();
            remaining = remaining[cut..].Trim();
        }

        if (remaining.Length > 0)
            yield return remaining;
    }

    private static int LastBreakOpportunity(string window)
    {
        var clauseBreak = Math.Max(window.LastIndexOf(", ", StringComparison.Ordinal), window.LastIndexOf("; ", StringComparison.Ordinal));

        if (clauseBreak > MaximumSegmentLength / 3)
            return clauseBreak + 1;

        var wordBreak = window.LastIndexOf(' ');

        if (wordBreak > 0)
            return wordBreak;

        return window.Length;
    }

    private static List<string> GroupIntoSegments(List<string> sentences)
    {
        var segments = new List<string>();

        if (sentences.Count == 0)
            return segments;

        segments.Add(sentences[0]);
        var current = new StringBuilder();

        foreach (var sentence in sentences.Skip(1))
        {
            if (current.Length > 0 && current.Length + 1 + sentence.Length > MaximumSegmentLength)
            {
                segments.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
                current.Append(' ');

            current.Append(sentence);
        }

        if (current.Length > 0)
            segments.Add(current.ToString());

        return segments;
    }

    [GeneratedRegex(@"^[ \t]*(?<fence>`{3,}|~{3,})[^\n]*\n.*?(?:^[ \t]*\k<fence>[ \t]*$|\z)", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FencedCodeBlock();

    [GeneratedRegex(@"^\s*\|.*\|\s*$")]
    private static partial Regex TableRow();

    [GeneratedRegex(@"^\s*([-*_])(\s*\1){2,}\s*$")]
    private static partial Regex HorizontalRule();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+")]
    private static partial Regex Heading();

    [GeneratedRegex(@"\s+#+\s*$")]
    private static partial Regex TrailingHeadingHashes();

    [GeneratedRegex(@"^\s*(>\s?)+")]
    private static partial Regex Blockquote();

    [GeneratedRegex(@"^\s*(?:[-*+•]|\d{1,3}[.)])\s+")]
    private static partial Regex ListMarker();

    [GeneratedRegex(@"^\[[ xX]\]\s+")]
    private static partial Regex TaskCheckbox();

    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Image();

    [GeneratedRegex(@"\[([^\]]+)\]\((?:[^()]|\([^)]*\))*\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"<https?://[^>]+>")]
    private static partial Regex AutoLink();

    [GeneratedRegex(@"`+([^`]+?)`+")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"(\*\*|__)(?=\S)(.+?)(?<=\S)\1")]
    private static partial Regex StrongEmphasis();

    [GeneratedRegex(@"(?<![\w*])\*(?=\S)(.+?)(?<=\S)\*(?![\w*])")]
    private static partial Regex StarEmphasis();

    [GeneratedRegex(@"(?<!\w)_(?=\S)(.+?)(?<=\S)_(?!\w)")]
    private static partial Regex UnderscoreEmphasis();

    [GeneratedRegex(@"~~(.+?)~~")]
    private static partial Regex Strikethrough();

    [GeneratedRegex(@"\bhttps?://[^\s)>\]]+")]
    private static partial Regex BareUrl();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"(?<=[.!?…][""'»«“”)\]]*)\s+")]
    private static partial Regex SentenceBoundary();
}
