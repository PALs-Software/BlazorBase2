using BlazorBase.Components.Help;
using Xunit;

namespace BlazorBase.Components.Test.Help;

public sealed class HelpStructureReaderTests
{
    private const string Page = """
        ---
        title: Blog
        ---
        # Blog

        Intro text.

        ## Tasks

        ### Publish a post

        Steps.

        ### Schedule a post

        More steps.

        ## Fields
        <!-- help:fields -->
        ### Post
        - **Title**: The headline of the post. Shown in lists.
        - **Status:** Draft or published. More *details* here.
        - **Slug**: URL part

        ## See also

        - **Not a field**: after the fields section.
        """;

    private static HelpProcessingContext CreateContext(IDictionary<string, string>? files = null) => new()
    {
        Topic = "pages/blog",
        ContentRootUrl = "help/en-US/",
        Culture = "en-US",
        LoadFileAsync = (path, _) => Task.FromResult(files is not null && files.TryGetValue(path, out var content) ? content : null),
    };

    private static async Task<HelpDocument> ProcessAsync(string markdown, IDictionary<string, string>? files = null) =>
        await HelpMarkdownProcessor.ProcessAsync(markdown, CreateContext(files));

    private static async Task<IReadOnlyList<HelpFieldEntry>> FieldsOfAsync(string entries)
    {
        var document = await ProcessAsync("# Page\n\n## Fields\n<!-- help:fields -->\n\n" + entries);
        return HelpStructureReader.Read(document).Fields;
    }

    private static async Task<string> ShortHtmlAsync(string entryText) =>
        Assert.Single(await FieldsOfAsync("- **Label**: " + entryText)).ShortHtml;

    [Fact]
    public async Task TheProcessor_RemovesTheMarkerAndRemembersItsLine()
    {
        var document = await ProcessAsync(Page);

        Assert.DoesNotContain("help:fields", document.Markdown);
        Assert.NotNull(document.FieldsStartLine);
        Assert.Equal("### Post", document.Markdown.Split('\n')[document.FieldsStartLine!.Value + 1]);
    }

    [Fact]
    public async Task WithoutAMarker_TheDocumentHasNoFieldsStartLine()
    {
        var document = await ProcessAsync("# Page\n\n## Fields\n\n- **Title**: Text.");

        Assert.Null(document.FieldsStartLine);
        Assert.Empty(HelpStructureReader.Read(document).Fields);
    }

    [Fact]
    public async Task AMarkerInsideADocsOnlyBlock_IsIgnored()
    {
        var document = await ProcessAsync("# Page\n\n<!-- Docs-Only-Start -->\n<!-- help:fields -->\n<!-- Docs-Only-End -->\n\n- **Title**: Text.");

        Assert.Null(document.FieldsStartLine);
    }

    [Fact]
    public async Task AMarkerInAnIncludedFile_IsFound()
    {
        var files = new Dictionary<string, string> { ["pages/includes/fields.md"] = "<!-- help:fields -->\n- **Title**: Included text." };

        var document = await ProcessAsync("# Page\n\nIntro\n\n## Fields\n\n[!INCLUDE [fields](includes/fields.md)]", files);

        Assert.Equal("Title", Assert.Single(HelpStructureReader.Read(document).Fields).Label);
    }

    [Fact]
    public async Task Headings_AreReadInOrderWithSlugIds()
    {
        var structure = HelpStructureReader.Read(await ProcessAsync(Page));

        Assert.Equal(
            ["tasks", "publish-a-post", "schedule-a-post", "fields", "post", "see-also"],
            structure.Headings.Select(heading => heading.Id));
        Assert.Equal([2, 3, 3, 2, 3, 2], structure.Headings.Select(heading => heading.Level));
    }

    [Fact]
    public async Task TheTableOfContents_NestsLevelThreeHeadingsUnderTheirLevelTwoHeading()
    {
        var toc = HelpStructureReader.Read(await ProcessAsync(Page)).TableOfContents;

        Assert.Equal(["Tasks", "Fields", "See also"], toc.Select(entry => entry.Heading.Text));
        Assert.Equal(["Publish a post", "Schedule a post"], toc[0].Children.Select(child => child.Text));
        Assert.Equal(["Post"], toc[1].Children.Select(child => child.Text));
        Assert.Empty(toc[2].Children);
    }

    [Fact]
    public async Task ALevelThreeHeadingWithoutALevelTwoHeading_IsATopLevelEntry()
    {
        var structure = HelpStructureReader.Read(await ProcessAsync("# Page\n\n### First\n\n### Second\n\n## Third"));

        Assert.Equal(["First", "Second", "Third"], structure.TableOfContents.Select(entry => entry.Heading.Text));
        Assert.All(structure.TableOfContents, entry => Assert.Empty(entry.Children));
    }

    [Theory]
    [InlineData("# Page\n\nOnly text.", false)]
    [InlineData("# Page\n\n## One\n\nText", false)]
    [InlineData("# Page\n\n## One\n\n#### Deep\n\nText", false)]
    [InlineData("# Page\n\n## One\n\n## Two", true)]
    [InlineData("# Page\n\n## One\n\n### Child", true)]
    public async Task TheTableOfContents_NeedsAtLeastTwoLevelTwoOrThreeHeadings(string markdown, bool expected) =>
        Assert.Equal(expected, HelpStructureReader.Read(await ProcessAsync(markdown)).HasTableOfContents);

    [Fact]
    public async Task RepeatedHeadings_GetUniqueIds()
    {
        var structure = HelpStructureReader.Read(await ProcessAsync("# Page\n\n## Options\n\n## Options\n\n### Options"));

        Assert.Equal(["options", "options-2", "options-3"], structure.Headings.Select(heading => heading.Id));
    }

    [Fact]
    public async Task GermanHeadings_UseTransliteratedIds()
    {
        var structure = HelpStructureReader.Read(await ProcessAsync("# Seite\n\n## Gut zu wissen\n\n## Größe für Übersicht\n\n## Aktionen"));

        Assert.Equal(["gut-zu-wissen", "groesse-fuer-uebersicht", "aktionen"], structure.Headings.Select(heading => heading.Id));
    }

    [Fact]
    public async Task HeadingText_IsPlainTextWithoutFormatting()
    {
        var structure = HelpStructureReader.Read(await ProcessAsync("# Page\n\n## The `Budget` and *boost* [rules](https://example.com)"));

        Assert.Equal("The Budget and boost rules", Assert.Single(structure.Headings).Text);
    }

    [Fact]
    public async Task Fields_AreTheTopLevelBoldLabelItemsBetweenTheMarkerAndTheNextLevelTwoHeading()
    {
        var fields = HelpStructureReader.Read(await ProcessAsync(Page)).Fields;

        Assert.Equal(["Title", "Status:", "Slug"], fields.Select(field => field.Label));
        Assert.Equal(["title", "status", "slug"], fields.Select(field => field.Key));
        Assert.Equal(["field-title", "field-status", "field-slug"], fields.Select(field => field.Id));
    }

    [Fact]
    public async Task ItemsBeforeTheMarkerNestedItemsAndItemsWithoutABoldLabel_AreNoFields()
    {
        var document = await ProcessAsync("""
            # Page

            - **Before**: Not a field.

            ## Fields
            <!-- help:fields -->
            - **First**: A field.
              - **Nested**: Not a field.
            - Plain item without a label.
            - *Italic*: Not a field.
            - **Last**: A field.
            """);

        Assert.Equal(["First", "Last"], HelpStructureReader.Read(document).Fields.Select(field => field.Label));
    }

    [Fact]
    public async Task FieldEntriesOfSeveralGroups_AreCollectedUntilTheNextLevelTwoHeading()
    {
        var document = await ProcessAsync("""
            # Page

            ## Fields
            <!-- help:fields -->
            ### Group A
            - **One**: First.
            ### Group B
            - **Two**: Second.
            ## See also
            - **Three**: Not a field.
            """);

        Assert.Equal(["One", "Two"], HelpStructureReader.Read(document).Fields.Select(field => field.Label));
    }

    [Fact]
    public async Task ARepeatedLabel_GetsAUniqueIdAndStaysInTheList()
    {
        var fields = await FieldsOfAsync("- **Name**: First.\n- **Name**: Second.");

        Assert.Equal(["field-name", "field-name-2"], fields.Select(field => field.Id));
        Assert.Equal(["First.", "Second."], fields.Select(field => field.ShortHtml));
    }

    [Theory]
    [InlineData("First one. Second one.", "First one.")]
    [InlineData("Is it so? Yes it is.", "Is it so?")]
    [InlineData("Stop! Then go.", "Stop!")]
    [InlineData("Ends here.", "Ends here.")]
    [InlineData("No sentence end", "No sentence end")]
    [InlineData("Use e.g. the tool. Next one.", "Use e.g. the tool.")]
    [InlineData("Version 1.5 is fine. Done.", "Version 1.5 is fine.")]
    [InlineData("lowercase. continues here", "lowercase. continues here")]
    [InlineData("Erster Satz. Zweiter Satz.", "Erster Satz.")]
    [InlineData("Über alles. Nichts sonst.", "Über alles.")]
    public async Task TheShortText_IsTheFirstSentenceOrTheWholeEntry(string text, string expected) =>
        Assert.Equal(expected, await ShortHtmlAsync(text));

    [Fact]
    public async Task TheShortText_KeepsBoldItalicAndCodeAndDropsLinks()
    {
        var html = await ShortHtmlAsync("Use **bold**, *italic* and `code` or [the docs](https://example.com/x) here. Second sentence.");

        Assert.Equal("Use <strong>bold</strong>, <em>italic</em> and <code>code</code> or the docs here.", html);
    }

    [Fact]
    public async Task TheShortText_IsHtmlEncoded()
    {
        var html = await ShortHtmlAsync("Mind <script>alert(1)</script> & \"quotes\". Second.");

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&amp;", html);
    }

    [Fact]
    public async Task TheColonAfterTheLabel_IsNotPartOfTheShortText()
    {
        Assert.Equal("Text after.", (await FieldsOfAsync("- **A**: Text after.")).Single().ShortHtml);
        Assert.Equal("Text after.", (await FieldsOfAsync("- **A:** Text after.")).Single().ShortHtml);
        Assert.Equal("Text after.", (await FieldsOfAsync("- **A** : Text after.")).Single().ShortHtml);
        Assert.Equal("Text after.", (await FieldsOfAsync("- **A** Text after.")).Single().ShortHtml);
    }

    [Fact]
    public async Task AnEntryWithoutText_HasAnEmptyShortHtml()
    {
        Assert.Equal(string.Empty, Assert.Single(await FieldsOfAsync("- **Only label**")).ShortHtml);
    }

    [Theory]
    [InlineData("Monthly budget", "monthly budget")]
    [InlineData("  Monthly   Budget  ", "monthly budget")]
    [InlineData("Monthly budget:", "monthly budget")]
    [InlineData("Monthly budget :", "monthly budget")]
    [InlineData("Name *", "name")]
    [InlineData("Name*:", "name")]
    [InlineData("Größe", "größe")]
    [InlineData(":", "")]
    [InlineData(null, "")]
    public void Normalize_TrimsCollapsesAndLowerCases(string? text, string expected) =>
        Assert.Equal(expected, HelpLabelKey.Normalize(text));

    [Fact]
    public async Task TheRenderedHtml_CarriesHeadingIdsAndFieldEntryIdsAndClass()
    {
        var context = CreateContext();
        var document = await HelpMarkdownProcessor.ProcessAsync(Page, context);

        var html = HelpMarkdownProcessor.ToHtml(document, context);

        Assert.Contains("<h2 id=\"tasks\">", html);
        Assert.Contains("<h3 id=\"publish-a-post\">", html);
        Assert.Contains("<h2 id=\"see-also\">", html);
        Assert.Matches("<li id=\"field-title\" class=\"help-field\">", html);
        Assert.Matches("<li id=\"field-status\" class=\"help-field\">", html);
        Assert.DoesNotContain("id=\"field-not-a-field\"", html);
        Assert.DoesNotContain("help:fields", html);
    }

    [Fact]
    public async Task TheRenderedIds_AgreeWithTheStructure()
    {
        var context = CreateContext();
        var document = await HelpMarkdownProcessor.ProcessAsync("# Page\n\n## Field name\n\n## Fields\n<!-- help:fields -->\n- **Name**: Text.\n- **Name**: Again.\n", context);
        var structure = HelpStructureReader.Read(document);

        var html = HelpMarkdownProcessor.ToHtml(document, context);

        foreach (var id in structure.Headings.Select(heading => heading.Id).Concat(structure.Fields.Select(field => field.Id)))
            Assert.Contains($"id=\"{id}\"", html);

        Assert.Equal(structure.Headings.Count + structure.Fields.Count, structure.Headings.Select(heading => heading.Id).Concat(structure.Fields.Select(field => field.Id)).Distinct().Count());
    }

    [Fact]
    public async Task WithoutPreparedRenderOptions_NoIdsAreWritten()
    {
        var context = CreateContext();
        var document = await HelpMarkdownProcessor.ProcessAsync(Page, context);

        var html = BlazorBase.Components.Html.MarkdownConverter.ToHtml(document.Markdown, HelpMarkdownProcessor.CreateRenderOptions(context));

        Assert.DoesNotContain(" id=\"", html);
    }
}
