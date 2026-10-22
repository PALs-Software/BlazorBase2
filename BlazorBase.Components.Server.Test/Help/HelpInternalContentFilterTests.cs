using BlazorBase.Components.Server.Help;
using Xunit;

namespace BlazorBase.Components.Server.Test.Help;

public sealed class HelpInternalContentFilterTests
{
    [Fact]
    public void ABlock_IsRemovedWithItsMarkers()
    {
        var result = HelpInternalContentFilter.Remove("Before\n<!-- Internal-Start -->\nsecret\n<!-- Internal-End -->\nAfter");

        Assert.Equal("Before\n\nAfter", result);
    }

    [Fact]
    public void SeveralBlocks_AreRemovedEachUpToTheirOwnEndMarker()
    {
        var result = HelpInternalContentFilter.Remove("A<!-- Internal-Start -->x<!-- Internal-End -->B<!-- Internal-Start -->\r\ny\r\n<!-- Internal-End -->C");

        Assert.Equal("ABC", result);
    }

    [Fact]
    public void AStartWithoutAnEnd_RemovesTheRestOfTheDocument()
    {
        var result = HelpInternalContentFilter.Remove("Visible\n<!-- Internal-Start -->\nsecret\nmore secret\n");

        Assert.Equal("Visible\n", result);
    }

    [Theory]
    [InlineData("<!--Internal-Start-->secret<!--Internal-End-->")]
    [InlineData("<!--   internal-start   -->secret<!-- INTERNAL-END -->")]
    [InlineData("<!-- Internal-Start\n-->secret<!--\nInternal-End -->")]
    public void SpellingVariants_AreRemovedToo(string markdown)
    {
        Assert.Equal("ab", HelpInternalContentFilter.Remove("a" + markdown + "b"));
    }

    [Fact]
    public void WithoutMarkers_TheTextIsUntouched()
    {
        const string markdown = "# Title\n\n<!-- Docs-Only-Start -->\ndetails\n<!-- Docs-Only-End -->\n";

        Assert.Same(markdown, HelpInternalContentFilter.Remove(markdown));
    }

    [Fact]
    public void AnEndMarkerAlone_IsLeftAlone()
    {
        Assert.Equal("a<!-- Internal-End -->b", HelpInternalContentFilter.Remove("a<!-- Internal-End -->b"));
    }
}
