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
