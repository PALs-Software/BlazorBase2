using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

public class BaseListTileMetricsTests
{
    private static PropertyColumnConfig<string> Column(string name) => new() { PropertyName = name, Property = text => text.Length };

    private static MobileCardPlan<string> Plan(bool subtitle = false, int badges = 0, int fields = 0) => new()
    {
        Title = Column("Title"),
        Subtitle = subtitle ? Column("Subtitle") : null,
        Badges = [.. Enumerable.Range(0, badges).Select(index => Column("Badge" + index))],
        Fields = [.. Enumerable.Range(0, fields).Select(index => Column("Field" + index))]
    };

    [Fact]
    public void ATitleOnly_NeedsAboutThreeAndAHalfLines()
    {
        Assert.Equal(4.1, BaseListTileMetrics.Resolve(Plan(), hasActions: false), 3);
    }

    [Fact]
    public void EveryPartTheTileShows_AddsItsOwnRoom()
    {
        var titleOnly = BaseListTileMetrics.Resolve(Plan(), hasActions: false);

        Assert.True(BaseListTileMetrics.Resolve(Plan(subtitle: true), false) > titleOnly);
        Assert.True(BaseListTileMetrics.Resolve(Plan(badges: 2), false) > titleOnly);
        Assert.True(BaseListTileMetrics.Resolve(Plan(fields: 1), false) > titleOnly);
        Assert.True(BaseListTileMetrics.Resolve(Plan(), true) > titleOnly);
    }

    [Fact]
    public void Fields_AreLaidOutTwoToARow()
    {
        var one = BaseListTileMetrics.Resolve(Plan(fields: 1), false);
        var two = BaseListTileMetrics.Resolve(Plan(fields: 2), false);
        var three = BaseListTileMetrics.Resolve(Plan(fields: 3), false);
        var four = BaseListTileMetrics.Resolve(Plan(fields: 4), false);

        Assert.Equal(one, two);
        Assert.Equal(three, four);
        Assert.True(three > two);
    }

    [Fact]
    public void ManyBadges_StayOnOneRow()
    {
        Assert.Equal(BaseListTileMetrics.Resolve(Plan(badges: 1), false), BaseListTileMetrics.Resolve(Plan(badges: 6), false));
    }

    [Fact]
    public void TheSlot_IncludesTheBorderAndTheGap()
    {
        Assert.Equal(10 * 16 + BaseListTileMetrics.BorderPixels + BaseListTileMetrics.GapPixels, BaseListTileMetrics.ToSlotPixels(10));
    }
}
