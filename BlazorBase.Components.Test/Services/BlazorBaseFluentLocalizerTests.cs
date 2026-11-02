using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BlazorBase.Components.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.Components.Test.Services;

public sealed class BlazorBaseFluentLocalizerTests
{
    private static readonly BlazorBaseFluentLocalizer Localizer = new();

    private static T InCulture<T>(string culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static List<string> FluentKeys()
    {
        using var stream = typeof(FluentButton).Assembly.GetManifestResourceStream("Microsoft.FluentUI.AspNetCore.Components.Localization.LanguageResource.resources")!;
        using var reader = new ResourceReader(stream);

        return reader.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Where(key => key != "Fake_Hello").ToList();
    }

    private static Dictionary<string, string> ReadResx(string fileName)
    {
        var directory = AppContext.BaseDirectory;

        while (directory is not null && !File.Exists(Path.Combine(directory, "BlazorBase.Components", "Localization", fileName)))
            directory = Path.GetDirectoryName(directory);

        Assert.NotNull(directory);

        return XDocument.Load(Path.Combine(directory, "BlazorBase.Components", "Localization", fileName)).Root!
            .Elements("data").ToDictionary(data => (string)data.Attribute("name")!, data => (string)data.Element("value")!);
    }

    [Fact]
    public void EveryFluentKey_HasANeutralAndAGermanText()
    {
        var neutral = ReadResx("FluentUiStrings.resx");
        var german = ReadResx("FluentUiStrings.de.resx");

        Assert.Empty(FluentKeys().Except(neutral.Keys));
        Assert.Empty(FluentKeys().Except(german.Keys));
        Assert.Empty(german.Keys.Except(neutral.Keys));
    }

    [Fact]
    public void EveryGermanText_KeepsThePlaceholdersOfTheNeutralText()
    {
        var neutral = ReadResx("FluentUiStrings.resx");
        var german = ReadResx("FluentUiStrings.de.resx");

        foreach (var (key, text) in neutral)
            Assert.Equal(
                Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).Order(),
                Regex.Matches(german[key], @"\{\d+\}").Select(match => match.Value).Order());
    }

    [Fact]
    public void German_AnswersTheRequiredMessageInGerman()
    {
        Assert.Equal("Dieses Feld ist erforderlich", InCulture("de", () => Localizer["FluentInputBase_RequiredMessage"]));
    }

    [Fact]
    public void English_AnswersTheTextOfTheFluentLibrary()
    {
        Assert.Equal("This field is required", InCulture("en", () => Localizer["FluentInputBase_RequiredMessage"]));
    }

    [Fact]
    public void AnUnsupportedCulture_AnswersEnglish()
    {
        Assert.Equal("This field is required", InCulture("fr", () => Localizer["FluentInputBase_RequiredMessage"]));
    }

    [Fact]
    public void German_FillsThePlaceholders()
    {
        Assert.Equal("Seite 2 von 5", InCulture("de", () => Localizer["Paginator_Status", 2, 5]));
    }

    [Fact]
    public void AKeyWithoutAText_AnswersItself()
    {
        Assert.Equal("Unknown_Key", InCulture("de", () => Localizer["Unknown_Key"]));
    }

    [Fact]
    public void AddBlazorBaseFluentUI_RegistersTheLocalizer()
    {
        var services = new ServiceCollection();
        services.AddBlazorBaseFluentUI();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<BlazorBaseFluentLocalizer>(provider.GetRequiredService<LibraryConfiguration>().Localizer);
    }
}
