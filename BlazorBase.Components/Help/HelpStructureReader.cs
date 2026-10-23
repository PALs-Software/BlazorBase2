using System.Text;
using System.Text.RegularExpressions;
using BlazorBase.Components.Html;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace BlazorBase.Components.Help;

/// <summary>
/// Reads the headings and the field entries of a help document and gives them their ids: every heading gets its
/// slug as <c>id</c>, every field entry <c>field-</c> plus the slug of its label, unique in the document.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Field entries are the top-level list items between the <c>&lt;!-- help:fields --&gt;</c> marker (see
/// <see cref="HelpDocument.FieldsStartLine"/>) and the next heading of level 2 (or 1) whose first paragraph starts
/// with a bold label. <c>- **Label**: text</c> and <c>- **Label:** text</c> both work.</item>
/// <item>The short text of an entry is its first sentence — up to a <c>.</c>, <c>!</c> or <c>?</c> followed by
/// white space and an upper-case letter, or by the end — or the whole text when there is no such end. It is
/// rendered as inline HTML with bold, italic, strikethrough and code; links, images and everything else become plain text.</item>
/// <item>The rendering of a document applies the ids through <see cref="TrustedMarkdownOptions.PrepareDocument"/>
/// (see <see cref="HelpMarkdownProcessor.CreateRenderOptions(HelpProcessingContext, HelpDocument)"/>), which uses the same
/// walk as <see cref="Read(HelpDocument)"/>, so the table of contents and the rendered ids always agree.</item>
/// </list>
/// </remarks>
public static partial class HelpStructureReader
{
    private const string FieldIdPrefix = "field-";
    private const string FieldClass = "help-field";

    private sealed record Piece(string Text, string Open, string Close);

    /// <summary>Reads the structure of <paramref name="document"/>.</summary>
    public static HelpDocumentStructure Read(HelpDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Read(document.Markdown, document.FieldsStartLine);
    }

    /// <summary>Reads the structure of <paramref name="markdown"/>, whose fields section starts at the zero-based line <paramref name="fieldsStartLine"/> (null: no fields section).</summary>
    public static HelpDocumentStructure Read(string markdown, int? fieldsStartLine)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        if (string.IsNullOrWhiteSpace(markdown))
            return HelpDocumentStructure.Empty;

        return Analyze(MarkdownConverter.ParseTrusted(markdown), fieldsStartLine, applyIds: false);
    }

    /// <summary>Walks <paramref name="document"/> and, with <paramref name="applyIds"/>, writes the ids and the field class into its blocks.</summary>
    internal static HelpDocumentStructure Analyze(MarkdownDocument document, int? fieldsStartLine, bool applyIds)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var headings = new List<HelpHeading>();
        var fields = new List<HelpFieldEntry>();
        var inFields = false;
        var fieldsDone = false;

        foreach (var block in document.Descendants<Block>())
        {
            if (!inFields && !fieldsDone && fieldsStartLine is { } start && block.Line >= start)
                inFields = true;

            if (block is HeadingBlock heading)
            {
                var text = CollapseWhitespace(PlainText(heading.Inline?.FirstChild));
                var id = HelpSlug.CreateUnique(text, used);

                if (applyIds)
                    heading.GetAttributes().Id = id;

                headings.Add(new HelpHeading(heading.Level, text, id));

                if (inFields && heading.Level <= 2)
                {
                    inFields = false;
                    fieldsDone = true;
                }

                continue;
            }

            if (inFields && block is ListBlock { Parent: MarkdownDocument } list)
                foreach (var item in list.OfType<ListItemBlock>())
                    if (ReadField(item, used, applyIds) is { } entry)
                        fields.Add(entry);
        }

        return new HelpDocumentStructure(headings, fields);
    }

    private static HelpFieldEntry? ReadField(ListItemBlock item, HashSet<string> used, bool applyIds)
    {
        if (item.FirstOrDefault() is not ParagraphBlock { Inline.FirstChild: EmphasisInline { DelimiterCount: 2 } strong })
            return null;

        var label = CollapseWhitespace(PlainText(strong.FirstChild));
        var key = HelpLabelKey.Normalize(label);

        if (key.Length == 0)
            return null;

        var id = HelpSlug.CreateUnique(label, used, FieldIdPrefix);

        if (applyIds)
        {
            var attributes = item.GetAttributes();
            attributes.Id = id;
            attributes.AddClass(FieldClass);
        }

        return new HelpFieldEntry(label, key, id, ShortHtml(strong.NextSibling));
    }

    private static string ShortHtml(Inline? first)
    {
        var pieces = new List<Piece>();
        Flatten(first, string.Empty, string.Empty, pieces);
        TrimLeading(pieces);

        var plain = string.Concat(pieces.Select(piece => piece.Text));
        var length = FirstSentenceLength(plain);

        while (length > 0 && char.IsWhiteSpace(plain[length - 1]))
            length--;

        return Render(pieces, length);
    }

    private static int FirstSentenceLength(string text)
    {
        var match = SentenceEnd().Match(text);
        return match.Success ? match.Index + 1 : text.Length;
    }

    private static void TrimLeading(List<Piece> pieces)
    {
        var colonSkipped = false;

        while (pieces.Count > 0)
        {
            var text = pieces[0].Text.TrimStart();

            if (!colonSkipped && text.StartsWith(':'))
            {
                colonSkipped = true;
                text = text[1..].TrimStart();
            }

            if (text.Length == 0)
            {
                pieces.RemoveAt(0);
                continue;
            }

            pieces[0] = pieces[0] with { Text = text };
            return;
        }
    }

    private static string Render(List<Piece> pieces, int length)
    {
        var builder = new StringBuilder();
        var remaining = length;
        string? open = null;
        var close = string.Empty;

        foreach (var piece in pieces)
        {
            var take = Math.Min(piece.Text.Length, remaining);

            if (take <= 0)
                break;

            if (open != piece.Open)
            {
                builder.Append(close).Append(piece.Open);
                open = piece.Open;
                close = piece.Close;
            }

            builder.Append(Escape(piece.Text[..take]));
            remaining -= take;
        }

        return builder.Append(close).ToString();
    }

    private static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string PlainText(Inline? first)
    {
        var pieces = new List<Piece>();
        Flatten(first, string.Empty, string.Empty, pieces);

        return string.Concat(pieces.Select(piece => piece.Text));
    }

    private static string CollapseWhitespace(string text) => Whitespace().Replace(text, " ").Trim();

    private static void Flatten(Inline? first, string open, string close, List<Piece> pieces)
    {
        for (var inline = first; inline is not null; inline = inline.NextSibling)
            switch (inline)
            {
                case LiteralInline literal:
                    pieces.Add(new Piece(literal.Content.ToString(), open, close));
                    break;
                case CodeInline code:
                    pieces.Add(new Piece(code.Content, open + "<code>", "</code>" + close));
                    break;
                case LineBreakInline:
                    pieces.Add(new Piece(" ", open, close));
                    break;
                case AutolinkInline autolink:
                    pieces.Add(new Piece(autolink.Url, open, close));
                    break;
                case HtmlEntityInline entity:
                    pieces.Add(new Piece(entity.Transcoded.ToString(), open, close));
                    break;
                case EmphasisInline emphasis:
                    var tag = EmphasisTag(emphasis);
                    Flatten(emphasis.FirstChild, open + "<" + tag + ">", "</" + tag + ">" + close, pieces);
                    break;
                case ContainerInline container:
                    Flatten(container.FirstChild, open, close, pieces);
                    break;
            }
    }

    private static string EmphasisTag(EmphasisInline emphasis) =>
        emphasis.DelimiterChar == '~' ? "del" : emphasis.DelimiterCount >= 2 ? "strong" : "em";

    [GeneratedRegex(@"[.!?](?=\s+\p{Lu}|\s*$)")]
    private static partial Regex SentenceEnd();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
