using System.Net;
using BlazorBase.Components.Server.Help;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace BlazorBase.Components.Server.Test.Help;

/// <summary>
/// End-to-end coverage for <c>MapBlazorBaseHelpContent</c> through a real <see cref="Microsoft.AspNetCore.TestHost.TestServer"/>:
/// anonymous callers get 401, signed-in users get Markdown without internal blocks and images, and nothing outside
/// the culture folder or of another type is ever served.
/// </summary>
public sealed class HelpContentEndpointMapperTests : IDisposable
{
    private readonly HelpContentFolder Folder = new();

    public void Dispose() => Folder.Dispose();

    private Task<HelpContentTestHost> StartAsync(Action<HelpContentEndpointOptions>? configure = null, bool useCookieAuthentication = false) =>
        HelpContentTestHost.StartAsync(endpoints => endpoints.MapBlazorBaseHelpContent(Folder.Root, configure), useCookieAuthentication);

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? user = "alice", string? roles = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (user is not null)
            request.Headers.Add(TestUserAuthenticationHandler.UserHeader, user);

        if (roles is not null)
            request.Headers.Add(TestUserAuthenticationHandler.RolesHeader, roles);

        return client.SendAsync(request);
    }

    [Theory]
    [InlineData("/help/en-US/general/blog.md")]
    [InlineData("/help/en-US/general/images/chart.png")]
    [InlineData("/help/en-US/general/does-not-exist.md")]
    [InlineData("/help/en-US/docfx.json")]
    public async Task AnAnonymousRequest_Gets401_WhateverIsAskedFor(string path)
    {
        using var host = await StartAsync();

        using var response = await GetAsync(host.CreateClient(), path, user: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AnAnonymousRequest_Gets401_NotARedirectToTheSignInPage_EvenWithCookieAuthenticationAndAFallbackPolicy()
    {
        using var host = await StartAsync(useCookieAuthentication: true);

        using var response = await host.CreateClient().GetAsync("/help/en-US/general/blog.md");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ASignedInUser_GetsTheMarkdown()
    {
        using var host = await StartAsync();

        using var response = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/markdown; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("private", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", response.Headers.GetValues(HeaderNames.XContentTypeOptions).Single());
        Assert.StartsWith("# Blog", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InternalBlocks_AreNeverDelivered()
    {
        using var host = await StartAsync();

        var blog = await (await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md")).Content.ReadAsStringAsync();
        var unterminated = await (await GetAsync(host.CreateClient(), "/help/en-US/general/unterminated.md")).Content.ReadAsStringAsync();

        Assert.Contains("Public text.", blog);
        Assert.Contains("More public text.", blog);
        Assert.DoesNotContain("Secret", blog);
        Assert.DoesNotContain("Internal", blog);
        Assert.Contains("Visible.", unterminated);
        Assert.DoesNotContain("Never delivered", unterminated);
        Assert.DoesNotContain("Neither", unterminated);
    }

    [Fact]
    public async Task Markdown_IsDeliveredAsUtf8WithoutAByteOrderMark()
    {
        using var host = await StartAsync();

        using var response = await GetAsync(host.CreateClient(), "/help/en-US/general/umlaut.md");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal("# Größe\n", System.Text.Encoding.UTF8.GetString(bytes));
        Assert.NotEqual(0xEF, bytes[0]);
    }

    [Fact]
    public async Task AnImage_IsServedWithItsContentTypeAndValidators()
    {
        using var host = await StartAsync();

        using var response = await GetAsync(host.CreateClient(), "/help/en-US/general/images/chart.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HelpContentFolder.PngBytes, await response.Content.ReadAsByteArrayAsync());
        Assert.NotNull(response.Headers.ETag);
        Assert.Contains("private", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task AnImageThatIsUnchanged_AnswersNotModified()
    {
        using var host = await StartAsync();
        var client = host.CreateClient();
        using var first = await GetAsync(client, "/help/en-US/general/images/chart.png");
        var request = new HttpRequestMessage(HttpMethod.Get, "/help/en-US/general/images/chart.png");
        request.Headers.Add(TestUserAuthenticationHandler.UserHeader, "alice");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);

        using var second = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task ASvg_IsServedSandboxedSoAScriptInItCannotRun()
    {
        using var host = await StartAsync();

        using var response = await GetAsync(host.CreateClient(), "/help/en-US/general/images/drawing.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("sandbox", response.Headers.GetValues(HeaderNames.ContentSecurityPolicy).Single());
        Assert.Equal("nosniff", response.Headers.GetValues(HeaderNames.XContentTypeOptions).Single());
    }

    [Theory]
    [InlineData("/help/en-US/../secret.md")]
    [InlineData("/help/en-US/general/../../secret.md")]
    [InlineData("/help/en-US/..\\secret.md")]
    [InlineData("/help/en-US/general/..%2F..%2Fsecret.md")]
    [InlineData("/help/en-US/%2e%2e/secret.md")]
    [InlineData("/help/en-US//secret.md")]
    [InlineData("/help/en-US/C:/Windows/win.ini")]
    [InlineData("/help/en-US/C:\\Windows\\win.ini")]
    [InlineData("/help/en-US//etc/passwd")]
    [InlineData("/help/../secret.md")]
    [InlineData("/help/..\\secret.md")]
    [InlineData("/help/en-US/general/blog.md::$DATA")]
    [InlineData("/help/en-US/_site/general/blog.md")]
    [InlineData("/help/en-US/general/.hidden.md")]
    public async Task APathThatLeavesTheCultureFolder_Gets404_ForASignedInUser(string rawPath)
    {
        using var host = await StartAsync();

        var context = await host.SendRawAsync(rawPath);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task AnEncodedTraversalInTheUrl_IsNeverServed()
    {
        using var host = await StartAsync();
        var client = host.CreateClient();

        foreach (var path in new[] { "/help/en-US/%2e%2e/secret.md", "/help/en-US/..%2fsecret.md", "/help/en-US/%2e%2e%2fsecret.md", "/help/en-US/general/%2e%2e/%2e%2e/secret.md" })
        {
            using var response = await GetAsync(client, path);

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("/help/en-US/docfx.json")]
    [InlineData("/help/en-US/toc.yml")]
    [InlineData("/help/en-US/general/notes.txt")]
    [InlineData("/help/en-US/general/page.html")]
    [InlineData("/help/en-US/general/blog")]
    [InlineData("/help/en-US/general/missing.md")]
    [InlineData("/help/fr-FR/general/blog.md")]
    [InlineData("/help/en-US")]
    [InlineData("/help/en-US/")]
    public async Task OtherFileTypesAndMissingFiles_Get404(string path)
    {
        using var host = await StartAsync();

        using var response = await GetAsync(host.CreateClient(), path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheRequestPath_CanBeChanged()
    {
        using var host = await StartAsync(options => options.RequestPath = "documentation/");

        using var moved = await GetAsync(host.CreateClient(), "/documentation/en-US/general/blog.md");
        using var old = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md");

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
    }

    [Fact]
    public async Task WithRoles_OnlyAUserInOneOfThemGetsTheContent()
    {
        using var host = await StartAsync(options => options.Roles = "Admin, Support");

        using var without = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md", roles: "Guest");
        using var other = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md");
        using var allowed = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md", roles: "Support");
        using var anonymous = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md", user: null);

        Assert.Equal(HttpStatusCode.Forbidden, without.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task WithAPolicy_OnlyAUserSatisfyingItGetsTheContent()
    {
        using var host = await StartAsync(options => options.PolicyName = HelpContentTestHost.EditorsPolicy);

        using var denied = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md", user: "alice");
        using var allowed = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md", user: HelpContentTestHost.EditorUser);
        using var anonymous = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md", user: null);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task AnExcludedFolder_CanBeAddedThroughTheOptions()
    {
        using var host = await StartAsync(options => options.ExcludedFolders.Add("general"));

        using var response = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EditsOnDisk_ShowWithoutARestart()
    {
        using var host = await StartAsync();
        Folder.Write("en-US/general/blog.md", "# Edited\n");

        using var response = await GetAsync(host.CreateClient(), "/help/en-US/general/blog.md");

        Assert.Equal("# Edited\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithoutAContentRoot_MappingFails()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => HelpContentTestHost.StartAsync(endpoints => endpoints.MapBlazorBaseHelpContent(options => options.RequestPath = "/help")));
    }
}
