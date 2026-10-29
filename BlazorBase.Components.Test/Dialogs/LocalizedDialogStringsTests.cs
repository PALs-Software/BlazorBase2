using System.Globalization;
using BlazorBase.Components.Dialogs;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.Components.Test.Dialogs;

/// <summary>
/// Strings the file and date dialogs produce themselves — validation messages and default button captions —
/// come from resources, so the German interface never shows English text from them.
/// </summary>
public class LocalizedDialogStringsTests : ComponentsBunitTestContextBase
{
    [Fact]
    public void TheFileDialog_ShowsGermanDefaultButtonCaptions()
    {
        using var culture = new CultureScope("de-DE");
        Services.AddLocalization();

        var dialog = DialogHarness<BaseFileInputDialog>.Open(this, "Datei", FileContent());

        var buttons = Captions(dialog.Content);
        Assert.Contains("Importieren", buttons);
        Assert.Contains("Abbrechen", buttons);
    }

    [Fact]
    public void TheFileDialog_ShowsEnglishDefaultButtonCaptions()
    {
        using var culture = new CultureScope("en-US");
        Services.AddLocalization();

        var dialog = DialogHarness<BaseFileInputDialog>.Open(this, "File", FileContent());

        var buttons = Captions(dialog.Content);
        Assert.Contains("Import", buttons);
        Assert.Contains("Cancel", buttons);
    }

    [Fact]
    public void ExplicitButtonCaptions_WinOverTheDefaults()
    {
        using var culture = new CultureScope("de-DE");
        Services.AddLocalization();

        var dialog = DialogHarness<BaseFileInputDialog>.Open(this, "Datei", new FileInputDialogContent
        {
            Message = "Pick",
            FileInputCaption = "File",
            FileFilter = ".xlsx",
            MaxFileSize = 1024,
            ConfirmButtonText = "Hochladen",
            CancelButtonText = "Zurück"
        });

        var buttons = Captions(dialog.Content);
        Assert.Contains("Hochladen", buttons);
        Assert.Contains("Zurück", buttons);
    }

    [Fact]
    public void TheDateDialog_ShowsGermanDefaultButtonCaptions()
    {
        using var culture = new CultureScope("de-DE");
        Services.AddLocalization();

        var dialog = DialogHarness<BaseDateInputDialog>.Open(this, "Datum", new DateInputDialogContent { Message = "Pick a date" });

        var buttons = Captions(dialog.Content);
        Assert.Contains("Bestätigen", buttons);
        Assert.Contains("Abbrechen", buttons);
    }

    [Theory]
    [InlineData(0L, "ist leer")]
    [InlineData(2048L, "maximal zulässige Größe")]
    public void TheValidator_ReturnsGermanMessages_WithTheDialogLocalizer(long size, string expected)
    {
        using var culture = new CultureScope("de-DE");
        var localizer = Localizer();

        var result = FileInputValidator.Validate("import.xlsx", size, ".xlsx", 1024, localizer);

        Assert.Contains(expected, result);
    }

    [Fact]
    public void TheValidator_ReturnsAGermanExtensionMessage_WithTheDialogLocalizer()
    {
        using var culture = new CultureScope("de-DE");

        var result = FileInputValidator.Validate("import.pdf", 10, ".xlsx", 1024, Localizer());

        Assert.Contains("Endung .xlsx", result);
    }

    [Fact]
    public void TheValidator_FallsBackToEnglish_WithoutALocalizer()
    {
        using var culture = new CultureScope("de-DE");

        var result = FileInputValidator.Validate("import.xlsx", 0, ".xlsx", 1024);

        Assert.Equal("The selected file is empty.", result);
    }

    private static List<string> Captions(IRenderedComponent<BaseFileInputDialog> content) =>
        content.FindComponents<FluentButton>().Select(button => button.Caption()).ToList();

    private static List<string> Captions(IRenderedComponent<BaseDateInputDialog> content) =>
        content.FindComponents<FluentButton>().Select(button => button.Caption()).ToList();

    private IStringLocalizer<BaseFileInputDialog> Localizer()
    {
        Services.AddLocalization();

        return Services.GetRequiredService<IStringLocalizer<BaseFileInputDialog>>();
    }

    private static FileInputDialogContent FileContent() => new()
    {
        Message = "Pick",
        FileInputCaption = "File",
        FileFilter = ".xlsx",
        MaxFileSize = 1024
    };

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo previous = CultureInfo.CurrentUICulture;

        public CultureScope(string name)
        {
            CultureInfo.CurrentUICulture = new CultureInfo(name);
            CultureInfo.CurrentCulture = new CultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentUICulture = previous;
            CultureInfo.CurrentCulture = previous;
        }
    }
}
