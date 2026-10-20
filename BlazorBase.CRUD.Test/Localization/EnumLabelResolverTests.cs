using BlazorBase.CRUD.Localization;
using Xunit;

namespace BlazorBase.CRUD.Test.Localization;

public class EnumLabelResolverTests
{
    [Theory]
    [InlineData("TypicalUser", "Typical user")]
    [InlineData("None", "None")]
    [InlineData("HTTPServer", "Http server")]
    [InlineData("Not_Set", "Not set")]
    [InlineData("", "")]
    public void Humanize_SplitsPascalCaseIntoWords(string identifier, string expected)
    {
        Assert.Equal(expected, EnumLabelResolver.Humanize(identifier));
    }
}
