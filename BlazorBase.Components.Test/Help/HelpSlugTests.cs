using BlazorBase.Components.Help;
using Xunit;

namespace BlazorBase.Components.Test.Help;

public sealed class HelpSlugTests
{
    [Theory]
    [InlineData("Good to know", "good-to-know")]
    [InlineData("  Tasks  ", "tasks")]
    [InlineData("Gut zu wissen", "gut-zu-wissen")]
    [InlineData("Größe für Änderungen", "groesse-fuer-aenderungen")]
    [InlineData("Übersicht öffnen", "uebersicht-oeffnen")]
    [InlineData("Café déjà vu", "cafe-deja-vu")]
    [InlineData("Step 1: Create (new) item!", "step-1-create-new-item")]
    [InlineData("--- Dashed ---", "dashed")]
    [InlineData("A/B & C", "a-b-c")]
    public void Create_ProducesALowerCaseHyphenatedSlug(string text, string expected) =>
        Assert.Equal(expected, HelpSlug.Create(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?!")]
    public void Create_FallsBackWhenNothingIsLeft(string? text) =>
        Assert.Equal(HelpSlug.Fallback, HelpSlug.Create(text));

    [Fact]
    public void CreateUnique_AppendsANumberForEveryRepeatedSlug()
    {
        var used = new HashSet<string>();

        var ids = new[] { "Options", "options", "OPTIONS!" }.Select(text => HelpSlug.CreateUnique(text, used)).ToList();

        Assert.Equal(["options", "options-2", "options-3"], ids);
    }

    [Fact]
    public void CreateUnique_AppliesThePrefixAndSharesTheSetWithOtherPrefixes()
    {
        var used = new HashSet<string>();

        Assert.Equal("field-name", HelpSlug.CreateUnique("Name", used, "field-"));
        Assert.Equal("field-name-2", HelpSlug.CreateUnique("Name", used, "field-"));
        Assert.Equal("field-name-3", HelpSlug.CreateUnique("Field name", used));
        Assert.Contains("field-name", used);
    }
}
