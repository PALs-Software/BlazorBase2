using BlazorBase.Components.Server.Help;
using Xunit;

namespace BlazorBase.Components.Server.Test.Help;

public sealed class HelpContentLocatorTests : IDisposable
{
    private readonly HelpContentFolder Folder = new();
    private readonly HelpContentLocator Locator;

    public HelpContentLocatorTests() => Locator = new HelpContentLocator(Folder.Root, ["_site", "obj", "bin"]);

    public void Dispose() => Folder.Dispose();

    [Fact]
    public void AMarkdownFile_IsLocatedWithItsContentType()
    {
        var file = Locator.Locate("en-US", "general/blog.md");

        Assert.NotNull(file);
        Assert.True(file.IsMarkdown);
        Assert.Equal("text/markdown; charset=utf-8", file.ContentType);
        Assert.Equal(Path.Combine(Path.GetFullPath(Folder.Root), "en-US", "general", "blog.md"), file.PhysicalPath);
    }

    [Theory]
    [InlineData("general/images/chart.png", "image/png")]
    [InlineData("general/images/drawing.svg", "image/svg+xml")]
    public void AnImage_IsLocatedWithItsContentType(string path, string contentType)
    {
        var file = Locator.Locate("en-US", path);

        Assert.NotNull(file);
        Assert.False(file.IsMarkdown);
        Assert.Equal(contentType, file.ContentType);
    }

    [Theory]
    [InlineData("jpg", "image/jpeg")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("gif", "image/gif")]
    [InlineData("webp", "image/webp")]
    public void EveryImageExtension_IsServed(string extension, string contentType)
    {
        Folder.Write("en-US/images/a." + extension, HelpContentFolder.PngBytes);

        Assert.Equal(contentType, Locator.Locate("en-US", "images/a." + extension)?.ContentType);
    }

    [Theory]
    [InlineData("../secret.md")]
    [InlineData("general/../../secret.md")]
    [InlineData("general/../blog.md")]
    [InlineData("./general/blog.md")]
    [InlineData("general/./blog.md")]
    [InlineData("..\\secret.md")]
    [InlineData("general\\blog.md")]
    [InlineData("/general/blog.md")]
    [InlineData("//general/blog.md")]
    [InlineData("general//blog.md")]
    [InlineData("general/blog.md/")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("%2e%2e/secret.md")]
    [InlineData("..%2Fsecret.md")]
    [InlineData("general/blog.md::$DATA")]
    [InlineData("general/blog.md\0.png")]
    [InlineData("general/.hidden.md")]
    [InlineData("_site/general/blog.md")]
    [InlineData("_SITE/chart.png")]
    [InlineData("general/blog.md ")]
    [InlineData("general/blog.md.")]
    [InlineData("")]
    [InlineData(null)]
    public void ARequestThatLeavesTheCultureFolderOrIsMalformed_FindsNothing(string? path)
    {
        Assert.Null(Locator.Locate("en-US", path));
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("en-US/..")]
    [InlineData("..\\en-US")]
    [InlineData("C:")]
    [InlineData("e")]
    [InlineData("en US")]
    public void AnInvalidCulture_FindsNothing(string? culture)
    {
        Assert.Null(Locator.Locate(culture, "general/blog.md"));
        Assert.Null(Locator.Locate(culture, "secret.md"));
    }

    [Theory]
    [InlineData("docfx.json")]
    [InlineData("toc.yml")]
    [InlineData("general/notes.txt")]
    [InlineData("general/page.html")]
    [InlineData("general/blog")]
    [InlineData("general")]
    public void OtherFileTypesAndFolders_FindNothing(string path)
    {
        Assert.Null(Locator.Locate("en-US", path));
    }

    [Fact]
    public void AMissingFileOrCulture_FindsNothing()
    {
        Assert.Null(Locator.Locate("en-US", "general/missing.md"));
        Assert.Null(Locator.Locate("fr-FR", "general/blog.md"));
    }

    [Fact]
    public void TheCultureIsPartOfTheLookup()
    {
        Assert.EndsWith(Path.Combine("de-DE", "general", "blog.md"), Locator.Locate("de-DE", "general/blog.md")?.PhysicalPath);
    }

    [Fact]
    public void WithoutAnExcludedFolderList_OnlyHiddenFoldersAreBlocked()
    {
        var open = new HelpContentLocator(Folder.Root);

        Assert.NotNull(open.Locate("en-US", "_site/general/blog.md"));
        Assert.Null(open.Locate("en-US", "general/.hidden.md"));
    }

    [Fact]
    public void ABlankContentRoot_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new HelpContentLocator(" "));
    }
}
