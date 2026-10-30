using BlazorBase.Dashboard.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace BlazorBase.Dashboard.Test.Cards;

public sealed class ListPartTests : DashboardTestContext
{
    private static RenderFragment<string> Row => item => builder => builder.AddMarkupContent(0, $"<span class=\"row\">{item}</span>");

    private IRenderedComponent<ListPart<string>> RenderList(Action<ComponentParameterCollectionBuilder<ListPart<string>>> configure) =>
        Render(configure);

    [Fact]
    public void ShowsTheFirstFiveItems_ByDefault()
    {
        var cut = RenderList(p => p
            .Add(l => l.Title, "Latest")
            .Add(l => l.Items, Enumerable.Range(1, 9).Select(number => $"Item {number}"))
            .Add(l => l.ItemTemplate, Row));

        var rows = cut.FindAll("li.lp-item .row");
        Assert.Equal(5, rows.Count);
        Assert.Equal("Item 1", rows[0].TextContent);
        Assert.Equal("Item 5", rows[4].TextContent);
    }

    [Fact]
    public void MaxItems_LimitsTheRows()
    {
        var cut = RenderList(p => p
            .Add(l => l.Items, new[] { "a", "b", "c", "d" })
            .Add(l => l.ItemTemplate, Row)
            .Add(l => l.MaxItems, 2));

        Assert.Equal(2, cut.FindAll("li.lp-item").Count);
    }

    [Fact]
    public void ItemsAreAListOfItems_WithTheTitleAsHeading()
    {
        var cut = RenderList(p => p.Add(l => l.Title, "Latest from the blog").Add(l => l.Items, new[] { "a" }).Add(l => l.ItemTemplate, Row));

        Assert.Equal("Latest from the blog", cut.Find("h2.dc-title").TextContent);
        Assert.Equal("UL", cut.Find(".lp-list").TagName);
    }

    [Fact]
    public void ShowAllLink_IsRenderedWithTheHref_AndTheDefaultText()
    {
        var cut = RenderList(p => p.Add(l => l.Items, new[] { "a" }).Add(l => l.ItemTemplate, Row).Add(l => l.ShowAllHref, "/Blog"));

        var link = cut.Find("a.dc-more");
        Assert.Equal("/Blog", link.GetAttribute("href"));
        Assert.Equal("Show all", link.TextContent.Trim());
    }

    [Fact]
    public void ShowAllText_IsUsedWhenSet_AndTotalCountExtendsTheDefault()
    {
        var custom = RenderList(p => p.Add(l => l.Items, new[] { "a" }).Add(l => l.ItemTemplate, Row).Add(l => l.ShowAllHref, "/Blog").Add(l => l.ShowAllText, "Blog"));
        var counted = RenderList(p => p.Add(l => l.Items, new[] { "a" }).Add(l => l.ItemTemplate, Row).Add(l => l.ShowAllHref, "/Boosts").Add(l => l.TotalCount, 3));

        Assert.Equal("Blog", custom.Find("a.dc-more").TextContent.Trim());
        Assert.Equal("Show all (3)", counted.Find("a.dc-more").TextContent.Trim());
    }

    [Fact]
    public void WithoutShowAllHref_NoLinkIsRendered()
    {
        var cut = RenderList(p => p.Add(l => l.Items, new[] { "a" }).Add(l => l.ItemTemplate, Row));

        Assert.Empty(cut.FindAll("a.dc-more"));
    }

    [Fact]
    public void NoItems_ShowTheDefaultEmptyText()
    {
        var cut = RenderList(p => p.Add(l => l.Items, Array.Empty<string>()).Add(l => l.ItemTemplate, Row));

        Assert.Equal("Nothing to show.", cut.Find(".lp-empty").TextContent);
        Assert.Empty(cut.FindAll(".lp-list"));
    }

    [Fact]
    public void NoItems_ShowTheEmptyTemplate_WhenGiven()
    {
        var cut = RenderList(p => p
            .Add(l => l.Items, (IEnumerable<string>?)null)
            .Add(l => l.ItemTemplate, Row)
            .Add(l => l.EmptyTemplate, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p class=\"custom-empty\">All caught up</p>"))));

        Assert.Equal("All caught up", cut.Find(".custom-empty").TextContent);
        Assert.Empty(cut.FindAll(".lp-empty"));
    }

    [Fact]
    public void Loading_ShowsSkeletonRows_AndMarksTheCardBusy()
    {
        var cut = RenderList(p => p.Add(l => l.Items, new[] { "a" }).Add(l => l.ItemTemplate, Row).Add(l => l.Loading, true));

        Assert.Equal(3, cut.FindAll(".lp-skeleton").Count);
        Assert.Empty(cut.FindAll(".row"));
        Assert.Equal("true", cut.Find("section.dc").GetAttribute("aria-busy"));
    }

    [Fact]
    public void EmptyText_IsGerman_InTheGermanCulture()
    {
        using var culture = new CultureScope("de-DE");

        var cut = RenderList(p => p.Add(l => l.Items, Array.Empty<string>()).Add(l => l.ItemTemplate, Row).Add(l => l.TotalCount, 4).Add(l => l.ShowAllHref, "/x"));

        Assert.Equal("Nichts anzuzeigen.", cut.Find(".lp-empty").TextContent);
        Assert.Equal("Alle anzeigen (4)", cut.Find("a.dc-more").TextContent.Trim());
    }

    [Fact]
    public void CollapseWhenEmpty_ReportsEmptyToTheHost_AndReportsContentAgain()
    {
        var host = new RecordingHost();
        var cut = Render<CascadingValue<IDashboardPartHost>>(p => p
            .Add(c => c.Value, host)
            .AddChildContent<ListPart<string>>(l => l
                .Add(x => x.Items, Array.Empty<string>())
                .Add(x => x.ItemTemplate, Row)
                .Add(x => x.CollapseWhenEmpty, true)));

        Assert.NotEmpty(host.EmptyReports);
        Assert.All(host.EmptyReports, reported => Assert.True(reported));

        cut.Render(p => p
            .Add(c => c.Value, host)
            .AddChildContent<ListPart<string>>(l => l
                .Add(x => x.Items, new[] { "a" })
                .Add(x => x.ItemTemplate, Row)
                .Add(x => x.CollapseWhenEmpty, true)));

        Assert.False(host.EmptyReports[^1]);
    }

    [Fact]
    public void WithoutCollapseWhenEmpty_TheHostIsNotTold()
    {
        var host = new RecordingHost();

        Render<CascadingValue<IDashboardPartHost>>(p => p
            .Add(c => c.Value, host)
            .AddChildContent<ListPart<string>>(l => l.Add(x => x.Items, Array.Empty<string>()).Add(x => x.ItemTemplate, Row)));

        Assert.Empty(host.EmptyReports);
    }
}
