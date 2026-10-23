using BlazorBase.CRUD.SavedFilters;
using Xunit;

namespace BlazorBase.CRUD.Test.SavedFilters;

public class ListPresetLinkTests
{
    [Fact]
    public void PlainPath_GetsTheQueryString() =>
        Assert.Equal("/Boosts?preset=my-pending", ListPresetLink.Build("/Boosts", "my-pending"));

    [Fact]
    public void ExistingQuery_IsKept_AndThePresetAppended() =>
        Assert.Equal("/Boosts?team=7&preset=failed", ListPresetLink.Build("/Boosts?team=7", "failed"));

    [Fact]
    public void ExistingPresetParameter_IsReplaced() =>
        Assert.Equal("/Boosts?a=1&b=2&preset=new", ListPresetLink.Build("/Boosts?a=1&preset=old&b=2", "new"));

    [Fact]
    public void ExistingPresetParameter_MatchedCaseInsensitively() =>
        Assert.Equal("/Boosts?preset=new", ListPresetLink.Build("/Boosts?Preset=old", "new"));

    [Fact]
    public void Id_IsUrlEncoded() =>
        Assert.Equal("/Boosts?preset=above%2090%25%26x%3D1", ListPresetLink.Build("/Boosts", "above 90%&x=1"));

    [Fact]
    public void Fragment_StaysAtTheEnd() =>
        Assert.Equal("/Boosts?preset=a#top", ListPresetLink.Build("/Boosts#top", "a"));

    [Fact]
    public void TrailingQuestionMark_IsHandled() =>
        Assert.Equal("/Boosts?preset=a", ListPresetLink.Build("/Boosts?", "a"));

    [Fact]
    public void AbsoluteUrl_IsSupported() =>
        Assert.Equal("https://host/Boosts?x=1&preset=a", ListPresetLink.Build("https://host/Boosts?x=1", "a"));

    [Fact]
    public void EmptyPath_IsTheRoot() =>
        Assert.Equal("/?preset=a", ListPresetLink.Build("", "a"));

    [Fact]
    public void EmptyId_ReturnsThePathUnchanged() =>
        Assert.Equal("/Boosts?x=1", ListPresetLink.Build("/Boosts?x=1", ""));

    [Fact]
    public void NullPath_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ListPresetLink.Build(null!, "a"));

    [Fact]
    public void BuiltLink_IsReadBackByTheQueryStringReader() =>
        Assert.Equal("above 90%", BlazorBase.Components.Routing.QueryStringReader.TryReadValue(ListPresetLink.Build("/x?q=1", "above 90%"), "preset"));
}
