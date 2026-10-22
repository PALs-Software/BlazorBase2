using BlazorBase.Components.Help;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace BlazorBase.Components.Test.Help;

public sealed class HelpRouteMapTests
{
    private static HelpRouteMap CreateMap(params (string Template, string Topic)[] routes) =>
        new(routes.Select(route => new KeyValuePair<string, string>(route.Template, route.Topic)));

    [Theory]
    [InlineData("", "pages/dashboard")]
    [InlineData("/", "pages/dashboard")]
    [InlineData("Blog", "pages/blog")]
    [InlineData("blog", "pages/blog")]
    [InlineData("/BLOG/", "pages/blog")]
    [InlineData("Blog?page=2#top", "pages/blog")]
    [InlineData("Blog/my-first-post", "pages/blog-post")]
    [InlineData("Blog/some%20slug", "pages/blog-post")]
    [InlineData("Users/12/edit", "pages/user-edit")]
    [InlineData("Files/a/b/c", "pages/files")]
    [InlineData("Files", "pages/files")]
    [InlineData("Reports", "pages/reports")]
    [InlineData("Reports/2026", "pages/reports")]
    public void ResolveTopic_MatchesTheRouteTemplates(string path, string expected)
    {
        var map = CreateMap(
            ("/", "pages/dashboard"),
            ("/Blog", "pages/blog"),
            ("/Blog/{Slug}", "pages/blog-post"),
            ("/Users/{id:int}/edit", "pages/user-edit"),
            ("/Files/{*path}", "pages/files"),
            ("/Reports/{year?}", "pages/reports"));

        Assert.Equal(expected, map.ResolveTopic(path));
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Blog/a/b")]
    [InlineData("Users/12")]
    public void ResolveTopic_ReturnsNullWithoutAMatch(string path)
    {
        var map = CreateMap(("/Blog", "pages/blog"), ("/Blog/{Slug}", "pages/blog-post"), ("/Users/{id:int}/edit", "pages/user-edit"));

        Assert.Null(map.ResolveTopic(path));
    }

    [Fact]
    public void ResolveTopic_PrefersLiteralSegmentsOverParameters()
    {
        var map = CreateMap(("/Items/{id}", "pages/item"), ("/Items/new", "pages/item-new"));

        Assert.Equal("pages/item-new", map.ResolveTopic("Items/new"));
        Assert.Equal("pages/item", map.ResolveTopic("Items/42"));
    }

    [Fact]
    public void GetRoute_ReturnsTheParameterlessRouteOfATopic()
    {
        var map = CreateMap(("/Blog/{Slug}", "pages/blog-post"), ("/Blog", "pages/blog"), ("/", "pages/dashboard"), ("/Reports/{year?}", "pages/reports"));

        Assert.Equal("/Blog", map.GetRoute("pages/blog"));
        Assert.Equal("/", map.GetRoute("pages/dashboard"));
        Assert.Equal("/Reports", map.GetRoute("pages/reports"));
        Assert.Null(map.GetRoute("pages/blog-post"));
        Assert.Null(map.GetRoute("pages/unknown"));
    }

    [Fact]
    public void GetRoute_PicksTheShortestOfSeveralRoutes()
    {
        var map = CreateMap(("/Dashboard/Overview", "pages/dashboard"), ("/", "pages/dashboard"));

        Assert.Equal("/", map.GetRoute("pages/dashboard"));
    }

    [Fact]
    public void Scan_FindsRoutablePagesWithATopicOnly()
    {
        var routes = HelpTopicResolver.Scan([typeof(HelpRouteMapTests).Assembly])
            .Where(route => route.Value.StartsWith("scan/", StringComparison.Ordinal))
            .Select(route => route.Key + " => " + route.Value)
            .Order()
            .ToList();

        Assert.Equal(["/scan/detail/{id} => scan/detail", "/scan/list => scan/list", "/scan/overview => scan/list"], routes);
    }

    [Fact]
    public void Resolver_ResolvesTopicsAndRoutesOfTheConfiguredAssemblies()
    {
        var options = new HelpOptions();
        options.Assemblies.Add(typeof(HelpRouteMapTests).Assembly);
        var resolver = new HelpTopicResolver(Microsoft.Extensions.Options.Options.Create(options));

        Assert.Equal("scan/detail", resolver.ResolveTopic("scan/detail/7"));
        Assert.Equal("scan/list", resolver.ResolveTopic("Scan/Overview"));
        Assert.Null(resolver.ResolveTopic("scan/untagged"));
        Assert.Equal("/scan/list", resolver.GetRoute("scan/list"));
        Assert.Null(resolver.GetRoute("scan/detail"));
    }

    [Route("/scan/list")]
    [Route("/scan/overview")]
    [HelpTopic("scan/list")]
    public sealed class ScannedListPage : ComponentBase
    {
    }

    [Route("/scan/detail/{id}")]
    [HelpTopic("scan/detail")]
    public sealed class ScannedDetailPage : ComponentBase
    {
    }

    [Route("/scan/untagged")]
    public sealed class ScannedUntaggedPage : ComponentBase
    {
    }

    [HelpTopic("scan/no-route")]
    public sealed class ScannedNoRoutePage : ComponentBase
    {
    }
}
