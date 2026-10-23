using System.Security.Claims;
using BlazorBase.Dashboard.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Dashboard.Test.Headline;

public sealed class HeadlinePartTests : DashboardTestContext
{
    private sealed class FixedProvider(params HeadlineInsight[] insights) : IHeadlineInsightProvider
    {
        public int Calls { get; private set; }

        public ValueTask<IReadOnlyList<HeadlineInsight>> GetInsightsAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult<IReadOnlyList<HeadlineInsight>>(insights);
        }
    }

    private sealed class ThrowingProvider : IHeadlineInsightProvider
    {
        public ValueTask<IReadOnlyList<HeadlineInsight>> GetInsightsAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("provider down");
    }

    private IRenderedComponent<HeadlinePart> RenderHeadline(Action<ComponentParameterCollectionBuilder<HeadlinePart>>? configure = null) =>
        Render<HeadlinePart>(parameters => configure?.Invoke(parameters));

    private static string CurrentInsight(IRenderedComponent<HeadlinePart> cut) => cut.Find(".hl-insight").TextContent.Trim();

    private static HeadlineInsight Insight(int priority, string text) => new(priority, text);

    [Fact]
    public void Greeting_UsesTheTimeOfDay_AndTheGivenName()
    {
        var cut = RenderHeadline(p => p.Add(h => h.UserName, "Patrick"));
        Assert.Equal("Good morning, Patrick", cut.Find(".hl-greeting").TextContent);

        Time.SetUtcNow(new DateTimeOffset(2026, 10, 23, 14, 0, 0, TimeSpan.Zero));
        Assert.Equal("Good afternoon, Patrick", RenderHeadline(p => p.Add(h => h.UserName, "Patrick")).Find(".hl-greeting").TextContent);

        Time.SetUtcNow(new DateTimeOffset(2026, 10, 23, 20, 0, 0, TimeSpan.Zero));
        Assert.Equal("Good evening, Patrick", RenderHeadline(p => p.Add(h => h.UserName, "Patrick")).Find(".hl-greeting").TextContent);

        Time.SetUtcNow(new DateTimeOffset(2026, 10, 23, 3, 0, 0, TimeSpan.Zero));
        Assert.Equal("Good evening, Patrick", RenderHeadline(p => p.Add(h => h.UserName, "Patrick")).Find(".hl-greeting").TextContent);
    }

    [Theory]
    [InlineData(4, 59, "Good evening")]
    [InlineData(5, 0, "Good morning")]
    [InlineData(11, 59, "Good morning")]
    [InlineData(12, 0, "Good afternoon")]
    [InlineData(17, 59, "Good afternoon")]
    [InlineData(18, 0, "Good evening")]
    public void GreetingBoundaries_AreFiveTwelveAndEighteenHundred(int hour, int minute, string expected)
    {
        Time.SetUtcNow(new DateTimeOffset(2026, 10, 23, hour, minute, 0, TimeSpan.Zero));

        var cut = RenderHeadline();

        Assert.Equal(expected, cut.Find(".hl-greeting").TextContent);
    }

    [Fact]
    public void Greeting_IsGerman_InTheGermanCulture()
    {
        using var culture = new CultureScope("de-DE");

        var cut = RenderHeadline(p => p.Add(h => h.UserName, "Patrick"));

        Assert.Equal("Guten Morgen, Patrick", cut.Find(".hl-greeting").TextContent);
    }

    [Fact]
    public void Name_ComesFromTheGivenNameClaim_WhenNoParameterIsSet()
    {
        Authorization.SetClaims(new Claim(ClaimTypes.GivenName, "Patrick"), new Claim("name", "Patrick Lehmann"));

        var cut = RenderHeadline();

        Assert.Equal("Good morning, Patrick", cut.Find(".hl-greeting").TextContent);
    }

    [Fact]
    public void Name_FallsBackToTheDisplayNameClaim()
    {
        Authorization.SetClaims(new Claim("name", "Patrick Lehmann"));

        var cut = RenderHeadline();

        Assert.Equal("Good morning, Patrick Lehmann", cut.Find(".hl-greeting").TextContent);
    }

    [Fact]
    public void LoginThatIsAnEmailAddress_IsNeverGreeted()
    {
        Authorization.SetAuthorized("patrick@example.com");

        var cut = RenderHeadline();

        Assert.Equal("Good morning", cut.Find(".hl-greeting").TextContent);
    }

    [Fact]
    public void ExplicitUserName_WinsOverTheClaims()
    {
        Authorization.SetClaims(new Claim(ClaimTypes.GivenName, "Claim"));

        var cut = RenderHeadline(p => p.Add(h => h.UserName, "Explicit"));

        Assert.Equal("Good morning, Explicit", cut.Find(".hl-greeting").TextContent);
    }

    [Fact]
    public void Insights_AreOrderedByPriority_AcrossProviders_AndLimitedToThree()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(40, "Fourth"), Insight(10, "First")));
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(30, "Third"), Insight(20, "Second")));

        var cut = RenderHeadline();

        Assert.Equal("First", CurrentInsight(cut));
        Assert.Equal(3, cut.FindAll(".hl-dot").Count);
    }

    [Fact]
    public void MaxInsights_CanBeChanged()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "A"), Insight(2, "B"), Insight(3, "C")));

        var cut = RenderHeadline(p => p.Add(h => h.MaxInsights, 2));

        Assert.Equal(2, cut.FindAll(".hl-dot").Count);
    }

    [Fact]
    public void BoldMarkers_BecomeStrongElements_AndHtmlIsEscaped()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "**3 budget boosts** are waiting <b>now</b>")));

        var cut = RenderHeadline();

        Assert.Equal("3 budget boosts", cut.Find(".hl-insight strong").TextContent);
        Assert.Equal("3 budget boosts are waiting <b>now</b>", CurrentInsight(cut));
        Assert.Empty(cut.FindAll(".hl-insight b"));
    }

    [Fact]
    public void InsightFromMarkup_IsRenderedAsGiven()
    {
        RenderFragment content = builder => builder.AddMarkupContent(0, "See <a href=\"/boosts\">boosts</a>");
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(new HeadlineInsight(1, content)));

        var cut = RenderHeadline();

        Assert.Equal("/boosts", cut.Find(".hl-insight a").GetAttribute("href"));
    }

    [Fact]
    public void ProviderThatThrows_DoesNotTakeTheOtherInsightsDown()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new ThrowingProvider());
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "Still here")));

        var cut = RenderHeadline();

        Assert.Equal("Still here", CurrentInsight(cut));
    }

    [Fact]
    public void SingleInsight_ShowsNoDots_AndStartsNoTimer()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "Only one")));

        var cut = RenderHeadline();

        Assert.Empty(cut.FindAll(".hl-dots"));
        Assert.Equal(0, Time.ActiveTimers);
    }

    [Fact]
    public void NoInsights_ShowOnlyTheGreeting()
    {
        var cut = RenderHeadline();

        Assert.Empty(cut.FindAll(".hl-insight"));
        Assert.NotEmpty(cut.FindAll(".hl-greeting"));
    }

    [Fact]
    public void Insights_RotateEveryEightSeconds_AndWrapAround()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "One"), Insight(2, "Two"), Insight(3, "Three")));
        var cut = RenderHeadline();
        cut.WaitForState(() => Time.ActiveTimers == 1);

        Time.Advance(TimeSpan.FromSeconds(8));
        cut.WaitForAssertion(() => Assert.Equal("Two", CurrentInsight(cut)));

        Time.Advance(TimeSpan.FromSeconds(8));
        cut.WaitForAssertion(() => Assert.Equal("Three", CurrentInsight(cut)));

        Time.Advance(TimeSpan.FromSeconds(8));
        cut.WaitForAssertion(() => Assert.Equal("One", CurrentInsight(cut)));
    }

    [Fact]
    public void RotationInterval_CanBeChanged_AndZeroSwitchesItOff()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "One"), Insight(2, "Two")));

        var off = RenderHeadline(p => p.Add(h => h.RotationInterval, TimeSpan.Zero));

        Assert.Equal(0, Time.ActiveTimers);
        Assert.Equal("One", CurrentInsight(off));
    }

    [Fact]
    public void Rotation_PausesWhilePointerIsOver_AndResumesAfterwards()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "One"), Insight(2, "Two")));
        var cut = RenderHeadline();
        cut.WaitForState(() => Time.ActiveTimers == 1);

        cut.Find("section.hl").MouseEnter();
        Time.Advance(TimeSpan.FromSeconds(8));
        Assert.Equal("One", CurrentInsight(cut));

        cut.Find("section.hl").MouseLeave();
        Time.Advance(TimeSpan.FromSeconds(8));
        cut.WaitForAssertion(() => Assert.Equal("Two", CurrentInsight(cut)));
    }

    [Fact]
    public void Rotation_PausesWhileFocusIsInside_AndResumesAfterwards()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "One"), Insight(2, "Two")));
        var cut = RenderHeadline();
        cut.WaitForState(() => Time.ActiveTimers == 1);

        cut.Find("section.hl").FocusIn();
        Time.Advance(TimeSpan.FromSeconds(8));
        Assert.Equal("One", CurrentInsight(cut));

        cut.Find("section.hl").FocusOut();
        Time.Advance(TimeSpan.FromSeconds(8));
        cut.WaitForAssertion(() => Assert.Equal("Two", CurrentInsight(cut)));
    }

    [Fact]
    public void Rotation_IsDisabled_WhenTheUserPrefersReducedMotion()
    {
        DashboardModule.Setup<bool>("prefersReducedMotion").SetResult(true);
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "One"), Insight(2, "Two")));

        var cut = RenderHeadline();

        cut.WaitForAssertion(() => Assert.Equal(0, Time.ActiveTimers));
        Time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal("One", CurrentInsight(cut));
        Assert.Equal(2, cut.FindAll(".hl-dot").Count);
    }

    [Fact]
    public void Dots_AreRealButtons_WithAccessibleNames_AndMarkTheCurrentInsight()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "One"), Insight(2, "Two"), Insight(3, "Three")));

        var cut = RenderHeadline();

        var dots = cut.FindAll("button.hl-dot");
        Assert.Equal(3, dots.Count);
        Assert.All(dots, dot => Assert.Equal("button", dot.GetAttribute("type")));
        Assert.Equal("Show insight 2 of 3", dots[1].GetAttribute("aria-label"));
        Assert.Equal("true", dots[0].GetAttribute("aria-current"));
        Assert.Null(dots[1].GetAttribute("aria-current"));
        Assert.Equal("group", cut.Find(".hl-dots").GetAttribute("role"));
    }

    [Fact]
    public void ClickingADot_ShowsThatInsight_AndStopsTheRotation()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "One"), Insight(2, "Two"), Insight(3, "Three")));
        var cut = RenderHeadline();
        cut.WaitForState(() => Time.ActiveTimers == 1);

        cut.FindAll("button.hl-dot")[2].Click();

        Assert.Equal("Three", CurrentInsight(cut));
        Assert.Equal("true", cut.FindAll("button.hl-dot")[2].GetAttribute("aria-current"));
        Assert.Equal(0, Time.ActiveTimers);
        Time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal("Three", CurrentInsight(cut));
    }

    [Fact]
    public void DotLabels_AreGerman_InTheGermanCulture()
    {
        using var culture = new CultureScope("de-DE");
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "Eins"), Insight(2, "Zwei")));

        var cut = RenderHeadline();

        Assert.Equal("Hinweis 2 von 2 anzeigen", cut.FindAll("button.hl-dot")[1].GetAttribute("aria-label"));
    }

    [Fact]
    public void ActionsSlot_IsRendered_InItsOwnContainer()
    {
        var cut = RenderHeadline(p => p.Add(h => h.Actions, (RenderFragment)(builder => builder.AddMarkupContent(0, "<a href=\"/boost\">Request budget boost</a>"))));

        Assert.Equal("/boost", cut.Find(".hl-actions a").GetAttribute("href"));
    }

    [Fact]
    public void NoActions_RenderNoActionsContainer()
    {
        var cut = RenderHeadline();

        Assert.Empty(cut.FindAll(".hl-actions"));
    }

    [Fact]
    public async Task Refresh_AsksTheProvidersAgain_AndKeepsTheShownInsightWhenItStillApplies()
    {
        var provider = new FixedProvider(Insight(1, "One"), Insight(2, "Two"));
        Services.AddSingleton<IHeadlineInsightProvider>(provider);
        var cut = RenderHeadline();
        cut.WaitForState(() => Time.ActiveTimers == 1);

        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        Assert.Equal(2, provider.Calls);
        Assert.Equal("One", CurrentInsight(cut));
    }

    [Fact]
    public async Task Dispose_StopsTheRotation()
    {
        Services.AddSingleton<IHeadlineInsightProvider>(new FixedProvider(Insight(1, "One"), Insight(2, "Two")));
        var cut = RenderHeadline();
        cut.WaitForState(() => Time.ActiveTimers == 1);

        await DisposeComponentsAsync();

        Assert.Equal(0, Time.ActiveTimers);
    }

    [Fact]
    public void HeadlineText_SplitsBoldSegments_AndKeepsUnpairedMarkersLiteral()
    {
        Assert.Equal([new HeadlineText.Segment("plain", false)], HeadlineText.Split("plain"));
        Assert.Equal(
            [new HeadlineText.Segment("a ", false), new HeadlineText.Segment("b", true), new HeadlineText.Segment(" c", false)],
            HeadlineText.Split("a **b** c"));
        Assert.Equal(
            [new HeadlineText.Segment("a ", false), new HeadlineText.Segment("**b", false)],
            HeadlineText.Split("a **b"));
        Assert.Equal(
            [new HeadlineText.Segment("b", true), new HeadlineText.Segment("c", true)],
            HeadlineText.Split("**b****c**"));
    }
}
