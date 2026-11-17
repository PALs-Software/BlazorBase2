using System.Text.RegularExpressions;
using BlazorBase.Components.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Components.Test.Layout;

/// <summary>
/// What a browser does with the layout styles cannot be seen in bUnit, so these tests pin the rules the touch
/// targets, the phone title and the navigation font depend on.
/// </summary>
public partial class BaseLayoutStylesTests
{
    private static string ReadStyles(params string[] relativePath) =>
        File.ReadAllText(Path.Combine(FluentStyleGuard.ProjectDirectory("BlazorBase.Components"), "Layout", Path.Combine(relativePath)));

    [Fact]
    public void HeaderControls_HaveA44PixelHitAreaWithoutNeedingACoarsePointer()
    {
        var styles = ReadStyles("BaseLayout.razor.css");

        Assert.Matches(HeaderControlSize(), styles);
        Assert.DoesNotContain("--base-header-control-size: 40px", styles);
    }

    [Fact]
    public void TheTitleReturnsOnAPhone_AndIsCutOffWithAnEllipsis_WhenTheClassIsSet()
    {
        var styles = ReadStyles("BaseLayout.razor.css");

        var rule = TitleOnPhoneRule().Match(styles);

        Assert.True(rule.Success, "The show-title-on-phone rule was not found.");
        Assert.Contains("display: block", rule.Groups["body"].Value);
        Assert.Contains("text-overflow: ellipsis", rule.Groups["body"].Value);
    }

    [Theory]
    [InlineData(@"\.base-side-nav ::deep \.base-side-nav-item\s*\{")]
    [InlineData(@"\.base-side-nav ::deep \.base-side-nav-text\s*\{")]
    public void SideNavigationEntries_TakeTheFontOfTheirSurroundings(string selectorPattern)
    {
        var styles = ReadStyles("Navigation", "BaseSideNavigation.razor.css");

        var rule = Regex.Match(styles, selectorPattern + @"(?<body>[^}]*)\}", RegexOptions.Singleline);

        Assert.True(rule.Success, $"No rule found for {selectorPattern}.");
        Assert.Contains("font-family: inherit", rule.Groups["body"].Value);
        Assert.Contains("font-size: inherit", rule.Groups["body"].Value);
    }

    [GeneratedRegex(@"--base-header-control-size:\s*44px;")]
    private static partial Regex HeaderControlSize();

    [GeneratedRegex(@"\.base-header\.show-title-on-phone \.base-header-title\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex TitleOnPhoneRule();
}
