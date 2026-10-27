using BlazorBase.Components.CommandPalette;
using Xunit;

namespace BlazorBase.Components.Test.CommandPalette;

public sealed class CommandPaletteTextTests
{
    [Theory]
    [InlineData("Übersicht", "ubersicht")]
    [InlineData("Café Crème", "cafe creme")]
    [InlineData("BUDGETS", "budgets")]
    [InlineData("", "")]
    public void Fold_IgnoresCaseAndDiacriticsAndKeepsTheLength(string input, string expected)
    {
        var folded = CommandPaletteText.Fold(input);

        Assert.Equal(expected, folded);
        Assert.Equal(input.Length, folded.Length);
    }

    [Fact]
    public void Tokenize_SplitsOnWhitespaceFoldsAndDropsDuplicates()
    {
        var tokens = CommandPaletteText.Tokenize("  Über   BUD über ");

        Assert.Equal(["uber", "bud"], tokens);
    }

    [Theory]
    [InlineData("budgets", "bud", "Starts", 0)]
    [InlineData("github copilot budgets", "bud", "WordStart", 15)]
    [InlineData("github copilot › budgets", "bud", "WordStart", 17)]
    [InlineData("subudget", "bud", "Contains", 2)]
    [InlineData("settings", "bud", "None", -1)]
    public void Match_ClassifiesStartsWithWordStartAndContains(string text, string token, string expectedKind, int expectedIndex)
    {
        var kind = CommandPaletteText.Match(text, token, out var index);

        Assert.Equal(expectedKind, kind.ToString());
        Assert.Equal(expectedIndex, index);
    }

    [Fact]
    public void Match_PrefersALaterWordStartOverAnEarlierContainsOccurrence()
    {
        var kind = CommandPaletteText.Match("rebudget budgets", "bud", out var index);

        Assert.Equal(MatchKind.WordStart, kind);
        Assert.Equal(9, index);
    }

    [Fact]
    public void Highlight_MarksTheMatchedRangesOfTheOriginalText()
    {
        var segments = CommandPaletteText.Highlight("Übersicht Budgets", CommandPaletteText.Tokenize("uber bud"));

        Assert.Equal(
            [new TextSegment("Über", true), new TextSegment("sicht ", false), new TextSegment("Bud", true), new TextSegment("gets", false)],
            segments);
    }

    [Fact]
    public void Highlight_MergesOverlappingRanges()
    {
        var segments = CommandPaletteText.Highlight("Budgets", CommandPaletteText.Tokenize("bud udge"));

        Assert.Equal([new TextSegment("Budge", true), new TextSegment("ts", false)], segments);
    }

    [Fact]
    public void Highlight_WithoutAMatchReturnsTheTextUntouched()
    {
        var segments = CommandPaletteText.Highlight("<b>Settings</b>", CommandPaletteText.Tokenize("zzz"));

        Assert.Equal([new TextSegment("<b>Settings</b>", false)], segments);
    }

    [Fact]
    public void Push_PutsTheNewestFirstWithoutDuplicatesAndCapsTheList()
    {
        IReadOnlyList<string> recent = ["a", "b", "c"];

        recent = CommandPaletteRecents.Push(recent, "c", 3);
        Assert.Equal(["c", "a", "b"], recent);

        recent = CommandPaletteRecents.Push(recent, "d", 3);
        Assert.Equal(["d", "c", "a"], recent);
    }
}
