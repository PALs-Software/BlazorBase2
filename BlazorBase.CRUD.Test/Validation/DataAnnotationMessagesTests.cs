using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Validation;

/// <summary>
/// The framework words DataAnnotations defaults in the UI language; a literal consumer message is looked up in the model
/// localizer and otherwise left alone.
/// </summary>
[Collection(CultureCollection.Name)]
public class DataAnnotationMessagesTests
{
    [Fact]
    public void Required_UsesTheEnglishTemplate_InAnEnglishUi()
    {
        using var _ = UiCulture.Use("en-US");

        var message = DataAnnotationMessages.Build(new RequiredAttribute(), "Title", "unused", FrameworkLocalizer(), null);

        Assert.Equal("The Title field is required.", message);
    }

    [Fact]
    public void Required_UsesTheGermanTemplate_InAGermanUi()
    {
        using var _ = UiCulture.Use("de-DE");

        var message = DataAnnotationMessages.Build(new RequiredAttribute(), "Titel", "The Titel field is required.", FrameworkLocalizer(), null);

        Assert.Equal("Das Feld „Titel“ ist erforderlich.", message);
    }

    [Fact]
    public void Range_SubstitutesLabelAndBounds()
    {
        using var _ = UiCulture.Use("de-DE");
        var attribute = new RangeAttribute(1, 10);

        var message = DataAnnotationMessages.Build(attribute, "Menge", "unused", FrameworkLocalizer(), null);

        Assert.Equal("Das Feld „Menge“ muss zwischen 1 und 10 liegen.", message);
    }

    [Fact]
    public void StringLength_WithMinimum_UsesTheRangeTemplate()
    {
        using var _ = UiCulture.Use("en-US");
        var attribute = new StringLengthAttribute(20) { MinimumLength = 5 };

        var message = DataAnnotationMessages.Build(attribute, "Code", "unused", FrameworkLocalizer(), null);

        Assert.Equal("The Code field must be between 5 and 20 characters long.", message);
    }

    [Fact]
    public void LiteralErrorMessage_WithoutModelLocalizer_KeepsTheAttributeMessage()
    {
        var attribute = new RequiredAttribute { ErrorMessage = "Please enter a name" };

        var message = DataAnnotationMessages.Build(attribute, "Name", "Please enter a name", FrameworkLocalizer(), null);

        Assert.Equal("Please enter a name", message);
    }

    [Fact]
    public void LiteralErrorMessage_IsTranslatedByTheModelLocalizer()
    {
        var attribute = new RequiredAttribute { ErrorMessage = "Please enter a name" };
        var modelLocalizer = Substitute.For<IStringLocalizer>();
        modelLocalizer["Please enter a name"].Returns(new LocalizedString("Please enter a name", "Bitte {0} eingeben", false));

        var message = DataAnnotationMessages.Build(attribute, "Name", "Please enter a name", FrameworkLocalizer(), modelLocalizer);

        Assert.Equal("Bitte Name eingeben", message);
    }

    [Fact]
    public void AttributeWithoutKnownTemplate_KeepsItsOwnMessage()
    {
        var attribute = new CustomAttribute();

        var message = DataAnnotationMessages.Build(attribute, "Name", "custom failure", FrameworkLocalizer(), null);

        Assert.Equal("custom failure", message);
    }

    private static IStringLocalizer FrameworkLocalizer() => TestFrameworkLocalizer.Create();

    private sealed class CustomAttribute : ValidationAttribute;
}

/// <summary>Creates the real framework resource localizer outside of a bUnit context.</summary>
internal static class TestFrameworkLocalizer
{
    public static IStringLocalizer Create() =>
        new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider()
            .GetRequiredService<IStringLocalizerFactory>()
            .Create(typeof(BlazorBaseCrudResources));
}

/// <summary>Serializes the tests that switch the thread UI culture.</summary>
[CollectionDefinition(Name)]
public sealed class CultureCollection
{
    public const string Name = "UiCulture";
}

/// <summary>Switches the current (UI) culture for the duration of a test and restores it afterwards.</summary>
internal sealed class UiCulture : IDisposable
{
    private readonly CultureInfo previousCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;

    private UiCulture(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static UiCulture Use(string name) => new(name);

    public void Dispose()
    {
        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUiCulture;
    }
}
