using BlazorBase.CRUD.Components;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// Where the row menu goes. It is <c>position: fixed</c>, so what lies outside the viewport can never be
/// scrolled into view: a menu opened in the lower third used to lose its last entries below the fold, and one
/// opened in the right half ran off the screen.
/// </summary>
public class BaseListContextMenuPlacementTests
{
    private const double MenuWidth = 200;
    private const double MenuHeight = 300;
    private const double ViewportWidth = 1000;
    private const double ViewportHeight = 800;

    [Fact]
    public void RoomBelowAndRight_OpensAtThePointer()
    {
        var placement = Place(ContextMenuAnchor.AtPoint(100, 100));

        Assert.Equal(100, placement.Left);
        Assert.Equal(100, placement.Top);
        Assert.Null(placement.MaxHeight);
    }

    [Fact]
    public void NoRoomBelow_FlipsAboveThePointer()
    {
        var placement = Place(ContextMenuAnchor.AtPoint(100, 700));

        Assert.Equal(400, placement.Top);
        Assert.Null(placement.MaxHeight);
    }

    [Fact]
    public void NoRoomRight_FlipsLeftOfThePointer()
    {
        var placement = Place(ContextMenuAnchor.AtPoint(900, 100));

        Assert.Equal(700, placement.Left);
    }

    [Fact]
    public void ABoxAnchorFlipsAcrossItsOwnEdges_SoTheMenuNeverCoversTheButton()
    {
        var button = new ContextMenuAnchor(Left: 940, Top: 700, Right: 980, Bottom: 740);

        var placement = Place(button);

        Assert.Equal(780, placement.Left);
        Assert.Equal(400, placement.Top);
    }

    [Fact]
    public void NoRoomOnEitherSide_IsClampedIntoTheViewport()
    {
        var narrowViewport = BaseListContextMenuPlacement.Compute(ContextMenuAnchor.AtPoint(50, 400), MenuWidth, MenuHeight, 220, 600);

        Assert.InRange(narrowViewport.Left, BaseListContextMenuPlacement.ViewportMargin, 12);
        Assert.True(narrowViewport.Top >= BaseListContextMenuPlacement.ViewportMargin);
        Assert.True(narrowViewport.Top + MenuHeight <= 600 - BaseListContextMenuPlacement.ViewportMargin);
    }

    [Fact]
    public void AMenuTallerThanTheViewport_IsCappedAndScrollsInsideItself()
    {
        var placement = BaseListContextMenuPlacement.Compute(ContextMenuAnchor.AtPoint(100, 100), MenuWidth, 900, ViewportWidth, 600);

        Assert.Equal(BaseListContextMenuPlacement.ViewportMargin, placement.Top);
        Assert.Equal(600 - (2 * BaseListContextMenuPlacement.ViewportMargin), placement.MaxHeight);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(999, 799)]
    [InlineData(500, 790)]
    [InlineData(995, 5)]
    public void TheMenuAlwaysEndsUpInsideTheViewport(double x, double y)
    {
        var placement = Place(ContextMenuAnchor.AtPoint(x, y));

        Assert.True(placement.Left >= BaseListContextMenuPlacement.ViewportMargin);
        Assert.True(placement.Left + MenuWidth <= ViewportWidth - BaseListContextMenuPlacement.ViewportMargin);
        Assert.True(placement.Top >= BaseListContextMenuPlacement.ViewportMargin);
        Assert.True(placement.Top + MenuHeight <= ViewportHeight - BaseListContextMenuPlacement.ViewportMargin);
    }

    private static ContextMenuPlacement Place(ContextMenuAnchor anchor)
        => BaseListContextMenuPlacement.Compute(anchor, MenuWidth, MenuHeight, ViewportWidth, ViewportHeight);
}
