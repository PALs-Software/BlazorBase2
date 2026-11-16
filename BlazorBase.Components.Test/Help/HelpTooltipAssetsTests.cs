using BlazorBase.Components.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Components.Test.Help;

/// <summary>
/// Guards the contract between <c>helpTooltips.js</c> and <c>blazorbase.css</c>. The DOM behavior itself (popover above an
/// open modal dialog, tooltips in short dialogs and dt/dd rows) has no script test infrastructure and was verified in a browser.
/// </summary>
public sealed class HelpTooltipAssetsTests
{
    private static readonly string WwwRoot = Path.Combine(FluentStyleGuard.ProjectDirectory("BlazorBase.Components"), "wwwroot");

    private static string Script => File.ReadAllText(Path.Combine(WwwRoot, "js", "helpTooltips.js"));

    private static string Styles => File.ReadAllText(Path.Combine(WwwRoot, "css", "blazorbase.css"));

    [Fact]
    public void ThePopover_IsAManualPopoverSoItSitsInTheTopLayerAboveModalDialogs()
    {
        var script = Script;

        Assert.Contains("setAttribute('popover', 'manual')", script);
        Assert.Contains("showPopover()", script);
        Assert.Contains("hidePopover()", script);
    }

    [Fact]
    public void ThePopoverStyles_ResetTheBrowserDefaultsThatWouldCenterTheTip()
    {
        var styles = Styles;
        var start = styles.IndexOf(".base-help-tip {", StringComparison.Ordinal);
        var rule = styles[start..styles.IndexOf('}', start)];

        Assert.Contains("inset: auto;", rule);
        Assert.Contains("margin: 0;", rule);
        Assert.Contains(".base-help-tip-link[hidden]", styles);
    }

    [Fact]
    public void OnPointerOver_DoesNotSkipATargetThatLiesInsideAPendingAncestorCandidate()
    {
        Assert.DoesNotContain("current.pending?.contains(target)", Script);
    }
}
