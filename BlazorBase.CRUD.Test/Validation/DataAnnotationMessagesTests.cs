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

    /// <summary>
    /// The DataType based attributes put their English default into <c>ErrorMessage</c> themselves; that is not a message
    /// the consumer wrote and must not stop the framework from translating it.
    /// </summary>
    [Fact]
    public void Url_UsesTheGermanTemplate_DespiteTheAttributeStoringItsEnglishDefault()
    {
        using var _ = UiCulture.Use("de-DE");
        var attribute = new UrlAttribute();

        var message = DataAnnotationMessages.Build(attribute, "Website-Basis-URL", attribute.ErrorMessage!, FrameworkLocalizer(), null);

        Assert.Equal("Das Feld „Website-Basis-URL“ enthält keine gültige URL.", message);
    }

    [Theory]
    [InlineData(typeof(EmailAddressAttribute), "Das Feld „Feld“ enthält keine gültige E-Mail-Adresse.")]
    [InlineData(typeof(PhoneAttribute), "Das Feld „Feld“ enthält keine gültige Telefonnummer.")]
    [InlineData(typeof(CreditCardAttribute), "Das Feld „Feld“ enthält keine gültige Kreditkartennummer.")]
    public void DataTypeAttributes_UseTheGermanTemplate(Type attributeType, string expected)
    {
        using var _ = UiCulture.Use("de-DE");
        var attribute = (ValidationAttribute)Activator.CreateInstance(attributeType)!;

        var message = DataAnnotationMessages.Build(attribute, "Feld", attribute.ErrorMessage!, FrameworkLocalizer(), null);

        Assert.Equal(expected, message);
    }

    [Fact]
    public void Compare_NamesTheOtherFieldByItsLocalizedLabel()
    {
        using var _ = UiCulture.Use("de-DE");
        var attribute = new CompareAttribute("Password");

        var message = DataAnnotationMessages.Build(
            attribute, "Bestätigung", attribute.ErrorMessage!, FrameworkLocalizer(), null, name => name == "Password" ? "Passwort" : name);

        Assert.Equal("Das Feld „Bestätigung“ stimmt nicht mit dem Feld „Passwort“ überein.", message);
    }

    [Fact]
    public void RegularExpression_UsesTheGermanTemplate()
    {
        using var _ = UiCulture.Use("de-DE");

        var message = DataAnnotationMessages.Build(new RegularExpressionAttribute("^a$"), "Code", "unused", FrameworkLocalizer(), null);

        Assert.Equal("Das Feld „Code“ hat ein ungültiges Format.", message);
    }

    [Fact]
    public void Url_WithAConsumerMessage_IsStillTranslatedByTheModelLocalizer()
    {
        var attribute = new UrlAttribute { ErrorMessage = "Please enter a web address" };
        var modelLocalizer = Substitute.For<IStringLocalizer>();
        modelLocalizer["Please enter a web address"].Returns(new LocalizedString("Please enter a web address", "Bitte eine Webadresse eingeben", false));

        var message = DataAnnotationMessages.Build(attribute, "Website", "Please enter a web address", FrameworkLocalizer(), modelLocalizer);

        Assert.Equal("Bitte eine Webadresse eingeben", message);
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
