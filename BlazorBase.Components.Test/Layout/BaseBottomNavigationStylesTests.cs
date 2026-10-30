using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace BlazorBase.Components.Test.Layout;

/// <summary>
/// A bottom bar tab label is cut off with an ellipsis, which needs <c>overflow: hidden</c>: with a line height of 1 the
/// descenders of "g", "p" and "(" fall outside the line box and are clipped away, so "Blog (1)" read as "Blo (1".
/// </summary>
public partial class BaseBottomNavigationStylesTests
{
    private const double MinimumLineHeight = 1.2;

    [Fact]
    public void TabLabel_HasALineHeightThatKeepsDescendersInsideTheClippedBox()
    {
        var styles = ReadStyles();

        var rule = LabelRule().Match(styles);

        Assert.True(rule.Success, "The tab label rule was not found.");
        var lineHeight = double.Parse(LineHeight().Match(rule.Groups["body"].Value).Groups["value"].Value, CultureInfo.InvariantCulture);
        Assert.True(lineHeight >= MinimumLineHeight, $"The label line height {lineHeight} clips descenders.");
    }

    private static string ReadStyles()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "BlazorBase.Components", "Layout", "Navigation", "BaseBottomNavigation.razor.css");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"BaseBottomNavigation.razor.css was not found above {AppContext.BaseDirectory}.");
    }

    [GeneratedRegex(@"::deep \.base-bottom-tab-label\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex LabelRule();

    [GeneratedRegex(@"line-height:\s*(?<value>[\d.]+)\s*;")]
    private static partial Regex LineHeight();
}
