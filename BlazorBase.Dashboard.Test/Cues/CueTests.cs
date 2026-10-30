using BlazorBase.Dashboard.Test.Infrastructure;
using Bunit;
using Xunit;

namespace BlazorBase.Dashboard.Test.Cues;

public sealed class CueTests : DashboardTestContext
{
    private IRenderedComponent<Cue> RenderCue(Action<ComponentParameterCollectionBuilder<Cue>> configure) => Render(configure);

    [Fact]
    public void CueWithHref_IsALink_WithTheTargetAndAccessibleName()
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "3").Add(c => c.Label, "Boosts to decide").Add(c => c.Href, "/PendingBudgetBoosts").Add(c => c.Style, CueStyle.Unfavorable));

        var link = cut.Find("a.cue");
        Assert.Equal("/PendingBudgetBoosts", link.GetAttribute("href"));
        Assert.Equal("3 Boosts to decide, unfavorable", link.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("div.cue"));
    }

    [Fact]
    public void CueWithoutHref_IsAPlainTile_NotALink()
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "1,240").Add(c => c.Label, "AI credits this month"));

        var tile = cut.Find("div.cue");
        Assert.Equal("group", tile.GetAttribute("role"));
        Assert.Equal("1,240 AI credits this month", tile.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("a"));
        Assert.Empty(cut.FindAll(".cue-go"));
    }

    [Theory]
    [InlineData(CueStyle.Favorable, "cue--favorable", ", favorable")]
    [InlineData(CueStyle.Ambiguous, "cue--ambiguous", ", needs attention")]
    [InlineData(CueStyle.Unfavorable, "cue--unfavorable", ", unfavorable")]
    [InlineData(CueStyle.Subordinate, "cue--subordinate", ", secondary")]
    [InlineData(CueStyle.None, "cue--none", "")]
    public void Style_IsAClass_AndReadOutInWords(CueStyle style, string cssClass, string words)
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "5").Add(c => c.Label, "Users").Add(c => c.Style, style).Add(c => c.Href, "/x"));

        var root = cut.Find(".cue");
        Assert.Contains(cssClass, root.ClassName);
        Assert.Equal("5 Users" + words, root.GetAttribute("aria-label"));
    }

    [Fact]
    public void StatusIsNotColourOnly_EveryStyleButNoneAndSubordinateShowsAnIcon()
    {
        foreach (var style in new[] { CueStyle.Favorable, CueStyle.Ambiguous, CueStyle.Unfavorable })
        {
            var cut = RenderCue(p => p.Add(c => c.Value, "1").Add(c => c.Label, "L").Add(c => c.Style, style));
            Assert.Single(cut.FindAll(".cue-status"));
        }

        Assert.Empty(RenderCue(p => p.Add(c => c.Value, "1").Add(c => c.Label, "L")).FindAll(".cue-status"));
    }

    [Fact]
    public void UnitAndSubValue_AreShown_AndPartOfTheAccessibleName()
    {
        var cut = RenderCue(p => p
            .Add(c => c.Value, "48")
            .Add(c => c.Unit, "/62")
            .Add(c => c.Label, "Active users")
            .Add(c => c.SubValue, "Projection $1,840")
            .Add(c => c.Wide, true));

        Assert.Equal("/62", cut.Find(".cue-unit").TextContent);
        Assert.Equal("Projection $1,840", cut.Find(".cue-sub").TextContent);
        Assert.Contains("cue--wide", cut.Find(".cue").ClassName);
        Assert.Equal("48/62 Active users, Projection $1,840", cut.Find(".cue").GetAttribute("aria-label"));
    }

    [Fact]
    public void UnitStartingWithALetter_IsSeparatedFromTheValueInTheAccessibleName()
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "14").Add(c => c.Unit, "min").Add(c => c.Label, "Since last sync"));

        Assert.Equal("14 min Since last sync", cut.Find(".cue").GetAttribute("aria-label"));
    }

    [Fact]
    public void Sparkline_IsDrawnFromThePoints_AndHiddenFromAssistiveTechnology()
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "$1,214").Add(c => c.Label, "Cost").Add(c => c.SparklinePoints, new[] { 1d, 2d, 3d }));

        var svg = cut.Find("svg.cue-sparkline");
        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.Equal("0,30 45,17 90,4", svg.QuerySelector("polyline")!.GetAttribute("points"));
    }

    [Fact]
    public void Sparkline_WithAFlatSeries_IsAHorizontalLine()
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "1").Add(c => c.Label, "L").Add(c => c.SparklinePoints, new[] { 5d, 5d }));

        Assert.Equal("0,17 90,17", cut.Find("polyline").GetAttribute("points"));
    }

    [Fact]
    public void Sparkline_WithLessThanTwoPoints_IsOmitted()
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "1").Add(c => c.Label, "L").Add(c => c.SparklinePoints, new[] { 5d }));

        Assert.Empty(cut.FindAll(".cue-sparkline"));
    }

    [Theory]
    [InlineData(72d, "72 100")]
    [InlineData(150d, "100 100")]
    [InlineData(-5d, "0 100")]
    [InlineData(33.333, "33.33 100")]
    public void Ring_ShowsThePercent_ClampedToZeroAndHundred(double percent, string dash)
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "72 %").Add(c => c.Label, "Budget used").Add(c => c.RingPercent, percent));

        var ring = cut.Find("svg.cue-ring");
        Assert.Equal("true", ring.GetAttribute("aria-hidden"));
        Assert.Equal(dash, ring.QuerySelector(".cue-ring-value")!.GetAttribute("stroke-dasharray"));
    }

    [Fact]
    public void WithoutRingOrSparkline_NeitherIsRendered()
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "1").Add(c => c.Label, "L"));

        Assert.Empty(cut.FindAll(".cue-ring"));
        Assert.Empty(cut.FindAll(".cue-sparkline"));
    }

    [Fact]
    public void Loading_ShowsASkeleton_InsteadOfTheValue_AndIsBusy()
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "3").Add(c => c.Label, "Boosts").Add(c => c.Href, "/x").Add(c => c.Loading, true));

        Assert.NotEmpty(cut.FindAll(".cue-skeleton"));
        Assert.Empty(cut.FindAll(".cue-value"));
        var link = cut.Find("a.cue");
        Assert.Equal("true", link.GetAttribute("aria-busy"));
        Assert.Equal("Boosts, loading", link.GetAttribute("aria-label"));
    }

    [Fact]
    public void ExtraClassAndAttributes_AreAppliedToTheRoot()
    {
        var cut = RenderCue(p => p.Add(c => c.Value, "1").Add(c => c.Label, "L").Add(c => c.Class, "mine").Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "cue-1" }));

        var root = cut.Find(".cue");
        Assert.Contains("mine", root.ClassName);
        Assert.Equal("cue-1", root.GetAttribute("data-testid"));
    }

    [Fact]
    public void StyleWords_AreGerman_InTheGermanCulture()
    {
        using var culture = new CultureScope("de-DE");

        var cut = RenderCue(p => p.Add(c => c.Value, "3").Add(c => c.Label, "Zu entscheiden").Add(c => c.Style, CueStyle.Unfavorable).Add(c => c.Href, "/x"));

        Assert.Equal("3 Zu entscheiden, ungünstig", cut.Find("a").GetAttribute("aria-label"));
    }

    [Fact]
    public void CueGroup_WrapsTheCues_AsALabelledGroup()
    {
        var cut = Render<CueGroup>(p => p
            .Add(g => g.Label, "To do")
            .AddChildContent<Cue>(c => c.Add(x => x.Value, "1").Add(x => x.Label, "One"))
            .AddChildContent<Cue>(c => c.Add(x => x.Value, "2").Add(x => x.Label, "Two")));

        var group = cut.Find(".cue-group");
        Assert.Equal("group", group.GetAttribute("role"));
        Assert.Equal("To do", group.GetAttribute("aria-label"));
        Assert.Equal(2, cut.FindAll(".cue").Count);
    }
}
