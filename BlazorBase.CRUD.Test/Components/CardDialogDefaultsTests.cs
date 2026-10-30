using BlazorBase.CRUD.Components;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

public class CardDialogDefaultsTests
{
    [Fact]
    public void Create_ReturnsAFluidWidth_CappedByTheViewport()
    {
        var parameters = CardDialogDefaults.Create();

        Assert.Equal("min(calc(100vw - 2rem), clamp(600px, 62vw, 1100px))", parameters.Width);
        Assert.Equal(CardDialogDefaults.Width, parameters.Width);
    }

    [Fact]
    public void Create_GrowsBeyondTheOldFixedWidth_ButNeverBeyondTheMaximum()
    {
        Assert.Equal("600px", CardDialogDefaults.MinimumWidth);
        Assert.Equal("1100px", CardDialogDefaults.MaximumWidth);
        Assert.Contains("clamp(600px,", CardDialogDefaults.Width);
        Assert.Contains("calc(100vw - 2rem)", CardDialogDefaults.Width);
    }

    [Fact]
    public void Create_UsesTheSharedCardDialogShape()
    {
        var options = CardDialogDefaults.Create();

        Assert.Null(options.Title);
        Assert.True(options.PreventDismissOnEscape);
        Assert.False(options.ShowCloseButton);
        Assert.True(options.Modal);
        Assert.Empty(options.Parameters);
    }

    [Fact]
    public void Create_ReturnsANewInstanceEveryTime()
    {
        var first = CardDialogDefaults.Create();
        first.Width = "10px";

        Assert.Equal(CardDialogDefaults.Width, CardDialogDefaults.Create().Width);
    }
}
