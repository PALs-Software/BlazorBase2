using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The number of columns of a card comes from its own width, not from the viewport. A 600px dialog on a wide desktop
/// used to get three 157px columns from a viewport query, which cut e-mail addresses and wrapped toggle labels.
/// </summary>
public partial class BaseCardColumnBreakpointTests
{
    private const double MinimumFieldWidth = 240;
    private const double ColumnGap = 16;

    [Fact]
    public void EveryContainerBreakpoint_GivesEachFieldAtLeastTheMinimumWidth()
    {
        var breakpoints = ContainerBreakpoints(ReadStyles("BaseCard.razor.css"));

        Assert.NotEmpty(breakpoints);

        foreach (var (minimumWidth, columns) in breakpoints)
        {
            var fieldWidth = (minimumWidth - ColumnGap * (columns - 1)) / columns;
            Assert.True(fieldWidth >= MinimumFieldWidth, $"{columns} columns from {minimumWidth}px leave {fieldWidth}px per field.");
        }
    }

    [Fact]
    public void ADialogCard_DoesNotOverrideTheColumnsFromTheViewport()
    {
        var cardStyles = ReadStyles("BaseCard.razor.css");

        var dialogRules = DialogRule().Matches(cardStyles).Select(rule => rule.Value).ToList();

        Assert.NotEmpty(dialogRules);
        Assert.DoesNotContain(dialogRules, rule => rule.Contains("grid-template-columns", StringComparison.Ordinal));
    }

    private static List<(double MinimumWidth, int Columns)> ContainerBreakpoints(string styles)
    {
        var result = new List<(double, int)>();

        foreach (Match block in ContainerBlock().Matches(styles))
        {
            var minimumWidth = double.Parse(block.Groups["width"].Value, CultureInfo.InvariantCulture);
            var columns = block.Groups["body"].Value.Contains("repeat(min(2,", StringComparison.Ordinal) ? 2 : 3;
            result.Add((minimumWidth, columns));
        }

        return result;
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

    [GeneratedRegex(@"\.base-dialog[^{}]*\{[^}]*\}")]
    private static partial Regex DialogRule();

    [GeneratedRegex(@"@container base-card \(min-width: (?<width>\d+)px\)\s*\{(?<body>.*?grid-template-columns:.*?)\}\s*\}", RegexOptions.Singleline)]
    private static partial Regex ContainerBlock();
}
