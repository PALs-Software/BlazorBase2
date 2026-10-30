using System.Text.RegularExpressions;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The sort bar of the tile layout sticks inside the page's scroll area, which has padding of its own: a plain
/// <c>top: 0</c> stops it that padding below the app header and lets tiles show through the gap above it. The list
/// script publishes the offset that compensates for it.
/// </summary>
public partial class BaseListCardViewStylesTests
{
    [Fact]
    public void SortBar_SticksAtTheOffsetThatCancelsThePagePadding()
    {
        var styles = ReadStyles("BaseListCardView.razor.css");

        var rule = ToolbarRule().Match(styles);

        Assert.True(rule.Success, "The sort bar rule was not found.");
        Assert.Contains("position: sticky", rule.Groups["body"].Value);
        Assert.Matches(@"top:\s*var\(--base-list-sticky-top,\s*0px\)", rule.Groups["body"].Value);
    }

    [Fact]
    public void SelectionBar_SticksAtTheBottomOfTheScrollArea_ThatEndsAboveTheAppBottomBar()
    {
        var styles = ReadStyles("BaseList.razor.css");

        var rule = Regex.Match(styles, @"\.base-list-selection-bar\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);

        Assert.True(rule.Success, "The selection bar rule was not found.");
        Assert.Contains("position: sticky", rule.Groups["body"].Value);
        Assert.Matches(@"bottom:\s*var\(--base-list-sticky-bottom,\s*0px\)", rule.Groups["body"].Value);
        Assert.Contains("env(safe-area-inset-bottom", rule.Groups["body"].Value);
    }

    [Fact]
    public void RowMenu_IsABottomSheetOnTheTileLayout()
    {
        var styles = ReadStyles("BaseList.razor.css");

        var rule = Regex.Match(styles, @"\.base-list-mobile \.context-menu\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);

        Assert.True(rule.Success, "The bottom sheet rule was not found.");
        Assert.Contains("bottom: 0", rule.Groups["body"].Value);
        Assert.Contains("left: 0 !important", rule.Groups["body"].Value);
        Assert.Contains("env(safe-area-inset-bottom", rule.Groups["body"].Value);
    }

    [Fact]
    public void MoreButtonOfATile_Is44PixelsAndInsideTheTopRightCorner()
    {
        var styles = ReadStyles("BaseListCardView.razor.css");

        var rule = Regex.Match(styles, @"\.base-list-card ::deep \.base-list-card-more\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);

        Assert.True(rule.Success, "The more button rule was not found.");
        Assert.Contains("grid-column: 3", rule.Groups["body"].Value);
        Assert.Contains("grid-row: 1", rule.Groups["body"].Value);
        Assert.Contains("min-width: 44px", rule.Groups["body"].Value);
        Assert.Contains("min-height: 44px", rule.Groups["body"].Value);
    }

    [Fact]
    public void SelectedTile_UsesTheAccentBorderAndTint_FromTheThemeTokens()
    {
        var styles = ReadStyles("BaseListCardView.razor.css");

        var rule = Regex.Match(styles, @"\.base-list-card\.is-selected\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);

        Assert.True(rule.Success, "The selected tile rule was not found.");
        Assert.Contains("--color-accent", rule.Groups["body"].Value);
        Assert.Contains("--color-bg-selected", rule.Groups["body"].Value);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|rgba?\(", rule.Groups["body"].Value);
    }

    private static string ReadStyles(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "BlazorBase.CRUD", "Components", fileName);

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"{fileName} was not found above {AppContext.BaseDirectory}.");
    }

    [GeneratedRegex(@"\.base-list-card-toolbar\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex ToolbarRule();
}
