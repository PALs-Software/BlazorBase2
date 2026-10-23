using BlazorBase.Dashboard.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace BlazorBase.Dashboard.Test.Cards;

public sealed class DashboardCardTests : DashboardTestContext
{
    [Fact]
    public void Card_IsASectionLabelledByItsTitle()
    {
        var cut = Render<DashboardCard>(p => p.Add(c => c.Title, "My AI usage").AddChildContent("<p>Body</p>"));

        var section = cut.Find("section.dc");
        var heading = cut.Find("h2.dc-title");
        Assert.Equal(heading.Id, section.GetAttribute("aria-labelledby"));
        Assert.Equal("My AI usage", heading.TextContent);
        Assert.Equal("Body", cut.Find(".dc-body p").TextContent);
    }

    [Fact]
    public void WithoutTitleAndLinks_NoHeaderIsRendered()
    {
        var cut = Render<DashboardCard>(p => p.AddChildContent("<p>Body</p>"));

        Assert.Empty(cut.FindAll(".dc-header"));
        Assert.Null(cut.Find("section.dc").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void MoreLink_HasTextAndHref_AndDefaultsToShowAll()
    {
        var custom = Render<DashboardCard>(p => p.Add(c => c.Title, "T").Add(c => c.MoreHref, "/stats").Add(c => c.MoreText, "My AI statistics"));
        var fallback = Render<DashboardCard>(p => p.Add(c => c.Title, "T").Add(c => c.MoreHref, "/stats"));

        Assert.Equal("/stats", custom.Find("a.dc-more").GetAttribute("href"));
        Assert.Equal("My AI statistics", custom.Find("a.dc-more").TextContent.Trim());
        Assert.Equal("Show all", fallback.Find("a.dc-more").TextContent.Trim());
    }

    [Fact]
    public void ToolsSlot_IsRenderedBeforeTheMoreLink()
    {
        var cut = Render<DashboardCard>(p => p
            .Add(c => c.Title, "T")
            .Add(c => c.MoreHref, "/x")
            .Add(c => c.Tools, (RenderFragment)(builder => builder.AddMarkupContent(0, "<span class=\"tool\">Month</span>"))));

        var end = cut.Find(".dc-header-end");
        Assert.True(end.InnerHtml.IndexOf("tool", StringComparison.Ordinal) < end.InnerHtml.IndexOf("dc-more", StringComparison.Ordinal));
    }

    [Fact]
    public void Loading_MarksTheCardBusy()
    {
        var cut = Render<DashboardCard>(p => p.Add(c => c.Title, "T").Add(c => c.Loading, true));

        Assert.Equal("true", cut.Find("section.dc").GetAttribute("aria-busy"));
    }
}
