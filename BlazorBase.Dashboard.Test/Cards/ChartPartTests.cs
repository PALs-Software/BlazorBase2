using BlazorBase.Dashboard.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBase.Dashboard.Test.Cards;

public sealed class ChartPartTests : DashboardTestContext
{
    private static RenderFragment<ChartPalette> Marker => palette => builder => builder.AddMarkupContent(0, $"<div class=\"chart-stub\" data-accent=\"{palette.Accent}\"></div>");

    [Fact]
    public void ChartContent_ReceivesThePalette_AndIsRenderedInsideTheFixedHeightArea()
    {
        var cut = Render<ChartPart>(p => p.Add(c => c.Title, "Cost trend").Add(c => c.ChartContent, Marker).Add(c => c.Height, "180px"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".cp-canvas .chart-stub")));
        Assert.Equal("currentColor", cut.Find(".chart-stub").GetAttribute("data-accent"));
        Assert.Contains("--chart-height: 180px", cut.Find(".cp-canvas").GetAttribute("style"));
    }

    [Fact]
    public void Palette_IsReadFromTheThemeTokens_ThroughTheScript()
    {
        DashboardModule.Setup<ChartPalette>("readChartPalette").SetResult(new ChartPalette { Accent = "#5BC5F2" });

        var cut = Render<ChartPart>(p => p.Add(c => c.ChartContent, Marker));

        cut.WaitForAssertion(() => Assert.Equal("#5BC5F2", cut.Find(".chart-stub").GetAttribute("data-accent")));
    }

    [Fact]
    public async Task ThemeChange_ReadsThePaletteAgain_AndRendersTheChartAgain()
    {
        var palettes = new Queue<ChartPalette>([new ChartPalette { Accent = "light" }, new ChartPalette { Accent = "dark" }]);
        var module = DashboardModule;
        module.Setup<ChartPalette>("readChartPalette").SetResult(palettes.Dequeue());
        module.Setup<int>("observeTheme", _ => true).SetResult(1);

        var cut = Render<ChartPart>(p => p.Add(c => c.ChartContent, Marker));
        cut.WaitForAssertion(() => Assert.Equal("light", cut.Find(".chart-stub").GetAttribute("data-accent")));
        var callback = ((DotNetObjectReference<DashboardJsCallback>)module.Invocations["observeTheme"].Single().Arguments[0]!).Value;

        module.Setup<ChartPalette>("readChartPalette").SetResult(palettes.Dequeue());
        await cut.InvokeAsync(() => callback.Invoke(true));

        cut.WaitForAssertion(() => Assert.Equal("dark", cut.Find(".chart-stub").GetAttribute("data-accent")));
    }

    [Fact]
    public void ChildContent_IsRenderedToo_ForChartsThatNeedNoThemeColours()
    {
        var cut = Render<ChartPart>(p => p.AddChildContent("<div class=\"plain-chart\"></div>"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".plain-chart")));
    }

    [Fact]
    public void ChartArea_IsAnImage_WithTheSummaryOrTheTitleAsName()
    {
        var withSummary = Render<ChartPart>(p => p.Add(c => c.Title, "Cost").Add(c => c.Summary, "Cost rose from 400 to 900").Add(c => c.ChartContent, Marker));
        var titleOnly = Render<ChartPart>(p => p.Add(c => c.Title, "Cost").Add(c => c.ChartContent, Marker));
        var neither = Render<ChartPart>(p => p.Add(c => c.ChartContent, Marker));

        Assert.Equal("img", withSummary.Find(".cp-canvas").GetAttribute("role"));
        Assert.Equal("Cost rose from 400 to 900", withSummary.Find(".cp-canvas").GetAttribute("aria-label"));
        Assert.Equal("Cost", titleOnly.Find(".cp-canvas").GetAttribute("aria-label"));
        Assert.Equal("Chart", neither.Find(".cp-canvas").GetAttribute("aria-label"));
    }

    [Fact]
    public void Loading_ShowsASkeleton_InsteadOfTheChart()
    {
        var cut = Render<ChartPart>(p => p.Add(c => c.ChartContent, Marker).Add(c => c.Loading, true));

        Assert.NotEmpty(cut.FindAll(".cp-skeleton"));
        Assert.Empty(cut.FindAll(".chart-stub"));
    }

    [Fact]
    public void MoreLink_AndTools_AreRenderedInTheHeader()
    {
        var cut = Render<ChartPart>(p => p
            .Add(c => c.Title, "Cost")
            .Add(c => c.MoreHref, "/TelemetryTrends")
            .Add(c => c.MoreText, "Telemetry trends")
            .Add(c => c.Tools, (RenderFragment)(builder => builder.AddMarkupContent(0, "<span class=\"tool\">Month</span>")))
            .Add(c => c.ChartContent, Marker));

        Assert.Equal("/TelemetryTrends", cut.Find("a.dc-more").GetAttribute("href"));
        Assert.Equal("Telemetry trends", cut.Find("a.dc-more").TextContent.Trim());
        Assert.NotNull(cut.Find(".dc-header-end .tool"));
    }

    [Fact]
    public void FillStyle_FillsTheChartArea() =>
        Assert.Equal("width: 100%; height: 100%;", ChartPart.FillStyle);

    [Fact]
    public async Task Dispose_StopsObservingTheTheme()
    {
        var module = DashboardModule;
        module.Setup<int>("observeTheme", _ => true).SetResult(7);
        Render<ChartPart>(p => p.Add(c => c.ChartContent, Marker));
        Services.GetRequiredService<DashboardInterop>();

        await DisposeComponentsAsync();

        Assert.Contains(module.Invocations, invocation => invocation.Identifier == "unobserve");
    }
}
