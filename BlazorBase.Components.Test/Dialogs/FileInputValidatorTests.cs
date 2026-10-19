using BlazorBase.Components.Dialogs;
using Xunit;

namespace BlazorBase.Components.Test.Dialogs;

/// <summary>
/// Kept independent of any Blazor/bUnit host: this is the same validation rules a server endpoint
/// enforces again for the actual request body, so the client side can only ever reject early, never
/// substitute for the authoritative check.
/// </summary>
public class FileInputValidatorTests
{
    [Fact]
    public void Validate_ReturnsError_WhenFileIsEmpty()
    {
        var result = FileInputValidator.Validate("import.xlsx", 0, ".xlsx", 1024);

        Assert.NotNull(result);
    }

    [Fact]
    public void Validate_ReturnsError_WhenFileExceedsMaxSize()
    {
        var result = FileInputValidator.Validate("import.xlsx", 2048, ".xlsx", 1024);

        Assert.NotNull(result);
        Assert.Contains("maximum allowed size", result);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenMaxSizeIsNotEnforced()
    {
        var result = FileInputValidator.Validate("import.xlsx", 10_000_000, ".xlsx", 0);

        Assert.Null(result);
    }

    [Fact]
    public void Validate_ReturnsError_WhenExtensionIsNotAllowed()
    {
        var result = FileInputValidator.Validate("import.pdf", 1024, ".xlsx", 1024 * 1024);

        Assert.NotNull(result);
        Assert.Contains("extension", result);
    }

    [Fact]
    public void Validate_IsCaseInsensitive_ForTheExtension()
    {
        var result = FileInputValidator.Validate("Import.XLSX", 1024, ".xlsx", 1024 * 1024);

        Assert.Null(result);
    }

    [Fact]
    public void Validate_AcceptsAnyOfSeveralAllowedExtensions()
    {
        var result = FileInputValidator.Validate("report.csv", 1024, ".csv,.txt", 1024 * 1024);

        Assert.Null(result);
    }

    [Fact]
    public void Validate_ReturnsNull_ForAnAcceptableFile()
    {
        var result = FileInputValidator.Validate("employees.xlsx", 20 * 1024 * 1024, ".xlsx", 20 * 1024 * 1024);

        Assert.Null(result);
    }

    [Fact]
    public void SplitFilter_TrimsAndIgnoresEmptyEntries()
    {
        var extensions = FileInputValidator.SplitFilter(" .csv ,, .txt");

        Assert.Equal([".csv", ".txt"], extensions);
    }
}
