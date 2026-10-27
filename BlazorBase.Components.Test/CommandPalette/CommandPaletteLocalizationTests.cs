using System.Collections;
using System.Globalization;
using System.Resources;
using BlazorBase.Components.CommandPalette;
using BlazorBase.Components.Layout;
using Xunit;

namespace BlazorBase.Components.Test.CommandPalette;

/// <summary>Every string the palette and the header search field show exists in English and German.</summary>
public sealed class CommandPaletteLocalizationTests
{
    [Theory]
    [InlineData(typeof(BaseCommandPalette))]
    [InlineData(typeof(BaseLayout))]
    public void GermanResources_CoverEveryEnglishKey(Type component)
    {
        var english = Read(component, CultureInfo.InvariantCulture);
        var german = Read(component, new CultureInfo("de"));

        Assert.NotEmpty(english);
        Assert.Empty(english.Keys.Except(german.Keys));
        Assert.Empty(german.Keys.Except(english.Keys));
        Assert.All(german, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
    }

    [Theory]
    [InlineData("Placeholder", "Seiten, Aktionen und Apps suchen…")]
    [InlineData("NoResults", "Keine Ergebnisse")]
    [InlineData("GroupPages", "Seiten")]
    [InlineData("GroupRecent", "Zuletzt verwendet")]
    public void GermanTexts_ArePresent(string key, string expected)
    {
        var german = Read(typeof(BaseCommandPalette), new CultureInfo("de"));

        Assert.Equal(expected, german[key]);
    }

    private static Dictionary<string, string> Read(Type component, CultureInfo culture)
    {
        var assembly = component.Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith($".{component.Name}.resources", StringComparison.Ordinal));
        var manager = new ResourceManager(resourceName[..^".resources".Length], assembly);
        var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!;

        return set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }
}
