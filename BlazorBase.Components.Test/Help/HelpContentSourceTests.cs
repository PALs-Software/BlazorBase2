using System.Net;
using System.Text;
using BlazorBase.Components.Help;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Components.Test.Help;

public sealed class HelpContentSourceTests
{
    private readonly Dictionary<string, (HttpStatusCode Status, string MediaType, string Content)> Files = new(StringComparer.Ordinal);
    private readonly List<string> Requests = [];
    private readonly HelpOptions Settings = new();

    public HelpContentSourceTests()
    {
        Settings.CultureFolders["en"] = "en-US";
        Settings.CultureFolders["de"] = "de-DE";
    }

    private HelpContentSource CreateSource() =>
        new(new HttpClient(new StubHandler(this)), new TestNavigationManager(), Options.Create(Settings));

    private void Serve(string path, string content, string mediaType = "text/markdown") =>
        Files["/" + path] = (HttpStatusCode.OK, mediaType, content);

    [Fact]
    public async Task GetTopic_ReadsTheFileOfTheCurrentCulture()
    {
        Serve("help/de-DE/pages/blog.md", "# Blog DE");
        Serve("help/en-US/pages/blog.md", "# Blog EN");

        var document = await CreateSource().GetTopicAsync("pages/blog", "de");

        Assert.Equal(new HelpSourceDocument("pages/blog", "de-DE", "# Blog DE"), document);
    }

    [Fact]
    public async Task GetTopic_FallsBackToTheDefaultCulture()
    {
        Serve("help/en-US/pages/blog.md", "# Blog EN");

        var document = await CreateSource().GetTopicAsync("pages/blog", "de-DE");

        Assert.Equal(new HelpSourceDocument("pages/blog", "en-US", "# Blog EN"), document);
    }

    [Fact]
    public async Task GetTopic_FallsBackToTheFallbackTopic_AfterTryingBothCultures()
    {
        Serve("help/de-DE/getting-started.md", "# Start DE");
        Serve("help/en-US/getting-started.md", "# Start EN");

        var document = await CreateSource().GetTopicAsync("pages/unknown", "de");

        Assert.Equal(new HelpSourceDocument("getting-started", "de-DE", "# Start DE"), document);
        Assert.Equal(
            ["/help/de-DE/pages/unknown.md", "/help/en-US/pages/unknown.md", "/help/de-DE/getting-started.md"],
            Requests);
    }

    [Fact]
    public async Task GetTopic_ReturnsNullWhenNothingExists()
    {
        Assert.Null(await CreateSource().GetTopicAsync("pages/unknown", "en"));
    }

    [Fact]
    public async Task GetTopic_TreatsAnHtmlAnswerAsMissing()
    {
        Serve("help/en-US/pages/blog.md", "<!doctype html><html></html>", "text/html");
        Serve("help/en-US/getting-started.md", "# Start");

        var document = await CreateSource().GetTopicAsync("pages/blog", "en");

        Assert.Equal("getting-started", document?.Topic);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GetTopic_TreatsRefusalsAndMissingFilesAsNoHelp(HttpStatusCode status)
    {
        Files["/help/en-US/pages/blog.md"] = (status, "text/plain", "refused");
        Files["/help/en-US/getting-started.md"] = (status, "text/plain", "refused");

        Assert.Null(await CreateSource().GetTopicAsync("pages/blog", "en"));
    }

    [Fact]
    public async Task AnUnauthorizedAnswer_IsNotRemembered_SoTheFileIsAskedForAgainAfterSignIn()
    {
        var source = CreateSource();
        Files["/help/en-US/pages/blog.md"] = (HttpStatusCode.Unauthorized, "text/plain", string.Empty);

        Assert.Null(await source.GetFileAsync("en-US", "pages/blog.md"));

        Serve("help/en-US/pages/blog.md", "# Blog");

        Assert.Equal("# Blog", await source.GetFileAsync("en-US", "pages/blog.md"));
    }

    [Fact]
    public async Task Files_AreFetchedOnce_EvenWhenTheyDoNotExist()
    {
        Serve("help/en-US/pages/blog.md", "# Blog");
        var source = CreateSource();

        await source.GetTopicAsync("pages/blog", "en");
        await source.GetTopicAsync("pages/blog", "en");
        await source.GetFileAsync("en-US", "includes/missing.md");
        await source.GetFileAsync("en-US", "includes/missing.md");

        Assert.Equal(["/help/en-US/pages/blog.md", "/help/en-US/includes/missing.md"], Requests);
    }

    [Theory]
    [InlineData("../secret.md")]
    [InlineData("/etc/passwd")]
    [InlineData("a\\b.md")]
    [InlineData("https://evil.test/x.md")]
    [InlineData("")]
    public async Task GetFile_RejectsPathsThatLeaveTheCultureFolder(string path)
    {
        Assert.Null(await CreateSource().GetFileAsync("en-US", path));
        Assert.Empty(Requests);
    }

    [Fact]
    public async Task GetFile_UsesTheConfiguredContentBasePath()
    {
        Settings.ContentBasePath = "/docs/help/";
        Serve("docs/help/en-US/includes/a.md", "A");

        Assert.Equal("A", await CreateSource().GetFileAsync("en-US", "includes/a.md"));
    }

    [Fact]
    public void ResolveCultureFolder_UsesTheMappingThenTheLanguageThenTheName()
    {
        Assert.Equal("en-US", Settings.ResolveCultureFolder("en"));
        Assert.Equal("de-DE", Settings.ResolveCultureFolder("de-AT"));
        Assert.Equal("fr-FR", Settings.ResolveCultureFolder("fr-FR"));
    }

    private sealed class StubHandler(HelpContentSourceTests owner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            owner.Requests.Add(path);

            if (!owner.Files.TryGetValue(path, out var file))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(file.Status)
            {
                Content = new StringContent(file.Content, Encoding.UTF8, file.MediaType),
            });
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://app.test/", "https://app.test/");
    }
}
