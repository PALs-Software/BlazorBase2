using BlazorBase.Components.Help;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BlazorBase.Components.Test.Help;

public sealed class HelpFieldIndexProviderTests
{
    private const string Markdown = """
        # Blog

        Intro.

        ## Fields
        <!-- help:fields -->
        - **Title**: The headline. Shown in lists.
        - **Status**: Draft or *published*. More.
        - **Empty**
        - **Title**: A duplicate. Ignored.

        ## See also
        """;

    private readonly IHelpContentSource ContentSource = Substitute.For<IHelpContentSource>();
    private readonly IHelpTopicResolver TopicResolver = Substitute.For<IHelpTopicResolver>();
    private readonly HelpFieldIndexProvider Provider;

    public HelpFieldIndexProviderTests()
    {
        TopicResolver.ResolveTopic("blog").Returns("pages/blog");
        TopicResolver.ResolveTopic("other").Returns("pages/other");
        ContentSource.GetTopicAsync("pages/blog", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("pages/blog", "en-US", Markdown));
        Provider = new HelpFieldIndexProvider(ContentSource, TopicResolver, Options.Create(new HelpOptions()));
    }

    [Fact]
    public async Task ThePageTopicsFieldEntries_BecomeTips()
    {
        var data = await Provider.GetForPathAsync("blog");

        Assert.NotNull(data);
        Assert.Equal("pages/blog", data.Topic);
        Assert.Equal(["title", "status"], data.Tips.Select(tip => tip.Key));
        Assert.Equal(["field-title", "field-status"], data.Tips.Select(tip => tip.Anchor));
        Assert.Equal("The headline.", data.Tips[0].Html);
        Assert.Equal("Draft or <em>published</em>.", data.Tips[1].Html);
    }

    [Fact]
    public async Task ADuplicateLabel_KeepsTheFirstEntry_AndAnEntryWithoutTextGivesNoTip()
    {
        var data = await Provider.GetForPathAsync("blog");

        Assert.Single(data!.Tips, tip => tip.Key == "title");
        Assert.DoesNotContain(data.Tips, tip => tip.Key == "empty");
    }

    [Fact]
    public async Task APageWithoutATopic_HasNoTips()
    {
        Assert.Null(await Provider.GetForPathAsync("unknown"));
        await ContentSource.DidNotReceive().GetTopicAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ATopicThatFallsBackToAnotherTopic_HasNoTips()
    {
        ContentSource.GetTopicAsync("pages/other", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("getting-started", "en-US", Markdown));

        Assert.Null(await Provider.GetForPathAsync("other"));
    }

    [Fact]
    public async Task UnreadableContent_HasNoTipsAndIsAskedForAgain()
    {
        ContentSource.GetTopicAsync("pages/other", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((HelpSourceDocument?)null, new HelpSourceDocument("pages/other", "en-US", Markdown));

        Assert.Null(await Provider.GetForPathAsync("other"));
        Assert.NotNull(await Provider.GetForPathAsync("other"));
    }

    [Fact]
    public async Task AReadTopic_IsBuiltOnlyOncePerScope()
    {
        await Provider.GetForPathAsync("blog");
        await Provider.GetForPathAsync("blog");

        await ContentSource.Received(1).GetTopicAsync("pages/blog", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ATopicWithoutAMarker_HasAnEmptyIndex()
    {
        ContentSource.GetTopicAsync("pages/other", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("pages/other", "en-US", "# Other\n\n- **Title**: Text."));

        var data = await Provider.GetForPathAsync("other");

        Assert.NotNull(data);
        Assert.Empty(data.Tips);
    }
}
