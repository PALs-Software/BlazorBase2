using System.Collections;
using System.Globalization;
using System.Resources;
using BlazorBase.Components.Diagnostics;
using BlazorBase.Components.HeaderActions;
using BlazorBase.Components.IssueReport;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.Components.Test.IssueReport;

public sealed class BaseIssueReportDialogTests : ComponentsBunitTestContextBase
{
    private readonly IIssueReportBrowser Browser = Substitute.For<IIssueReportBrowser>();
    private readonly IssueReportOptions Options = new() { Recipient = "support@example.com", SiteName = "demo", AppVersion = "1.2.3" };
    private readonly ClientDiagnostics Diagnostics = new(Microsoft.Extensions.Options.Options.Create(new ClientDiagnosticsOptions()));

    private readonly EnglishCulture English = new();

    public BaseIssueReportDialogTests()
    {
        Services.AddLocalization();
        AddAuthorization();
        Browser.GetUserAgentAsync().Returns(new ValueTask<string>("TestBrowser/1.0"));
        Browser.OpenMailtoAsync(Arg.Any<string>()).Returns(new ValueTask<bool>(true));
        Browser.CopyTextAsync(Arg.Any<string>()).Returns(new ValueTask<bool>(true));
        Services.AddSingleton(Browser);
        Services.AddSingleton<IOptions<IssueReportOptions>>(Microsoft.Extensions.Options.Options.Create(Options));
        Services.AddSingleton<IClientDiagnostics>(Diagnostics);
        Services.AddScoped<IIssueReportDiagnosticsCollector, IssueReportDiagnosticsCollector>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            English.Dispose();

        base.Dispose(disposing);
    }

    private IRenderedComponent<BaseIssueReportDialog> RenderDialog(IssueReportDialogContent? content = null) =>
        Render<BaseIssueReportDialog>(parameters => parameters.Add(p => p.Content, content ?? new IssueReportDialogContent()));

    private static void Fill(IRenderedComponent<BaseIssueReportDialog> cut, string summary, string? description = null)
    {
        cut.Find("fluent-text-field").Change(summary);
        if (description is not null)
            cut.Find("fluent-text-area").Change(description);
    }

    [Fact]
    public void TheDialogShowsTheFieldsAndTheDiagnosticsPreview()
    {
        Diagnostics.RecordError("Test", new InvalidOperationException("broken"));
        Diagnostics.RecordCorrelationId("corr-7");

        var cut = RenderDialog();

        Assert.NotNull(cut.Find("fluent-text-field"));
        Assert.NotNull(cut.Find("fluent-text-area"));
        Assert.NotNull(cut.Find("fluent-select"));
        var preview = cut.Find("pre.base-issue-diagnostics").TextContent;
        Assert.Contains("App=demo 1.2.3", preview);
        Assert.Contains("Correlation=corr-7", preview);
        Assert.Contains("InvalidOperationException: broken", preview);
        Assert.Contains("Browser=TestBrowser/1.0", preview);
    }

    [Fact]
    public void TheSubmitButtonNeedsASummary()
    {
        var cut = RenderDialog();

        Assert.True(cut.Find("fluent-button[type=submit]").HasAttribute("disabled"));

        Fill(cut, "Something broke");

        Assert.False(cut.Find("fluent-button[type=submit]").HasAttribute("disabled"));
    }

    [Fact]
    public void WithoutACategoryListTheCategoryFieldIsLeftOut()
    {
        Options.Categories = [];

        var cut = RenderDialog();

        Assert.Empty(cut.FindAll("fluent-select"));
    }

    [Fact]
    public void OpeningTheMailProgramSendsTheLinkAndShowsAClosingConfirmation()
    {
        var cut = RenderDialog();
        Fill(cut, "Something broke", "It did not work");

        cut.Find("form").Submit();

        Browser.Received(1).OpenMailtoAsync(Arg.Is<string>(uri =>
            uri.StartsWith("mailto:support@example.com?subject=", StringComparison.Ordinal)
            && Uri.UnescapeDataString(uri).Contains("[demo] Something broke")
            && Uri.UnescapeDataString(uri).Contains("It did not work")
            && Uri.UnescapeDataString(uri).Contains("Browser=TestBrowser/1.0")));
        Assert.Contains("mail program should open", cut.Find(".base-issue-result").TextContent);
        Assert.Empty(cut.FindAll("form"));
        Assert.Contains("Close", cut.Find(".base-issue-actions fluent-button").TextContent);
    }

    [Fact]
    public void SwitchingDiagnosticsOffRemovesThemFromThePreviewAndTheLink()
    {
        var cut = RenderDialog();
        Fill(cut, "Something broke", "Text");

        cut.InvokeAsync(() => cut.FindComponent<FluentCheckbox>().Instance.ValueChanged.InvokeAsync(false));
        Assert.Empty(cut.FindAll("pre.base-issue-diagnostics"));
        cut.Find("form").Submit();

        Browser.Received(1).OpenMailtoAsync(Arg.Is<string>(uri => !Uri.UnescapeDataString(uri).Contains("Browser=") && !Uri.UnescapeDataString(uri).Contains("Diagnostics")));
    }

    [Fact]
    public void WhenTheMailProgramDoesNotOpenTheDetailsAreCopiedAndTheRecipientIsNamed()
    {
        Browser.OpenMailtoAsync(Arg.Any<string>()).Returns(new ValueTask<bool>(false));
        var cut = RenderDialog();
        Fill(cut, "Something broke", "Text");

        cut.Find("form").Submit();

        Browser.Received(1).CopyTextAsync(Arg.Is<string>(text => text.Contains("To: support@example.com") && text.Contains("Browser=TestBrowser/1.0")));
        var result = cut.Find(".base-issue-result").TextContent;
        Assert.Contains("did not open", result);
        Assert.Contains("support@example.com", result);
        Assert.Contains("copied", result);
    }

    [Fact]
    public void WhenCopyingFailsTheFullTextIsOfferedForManualCopy()
    {
        Browser.OpenMailtoAsync(Arg.Any<string>()).Returns(new ValueTask<bool>(false));
        Browser.CopyTextAsync(Arg.Any<string>()).Returns(new ValueTask<bool>(false));
        var cut = RenderDialog();
        Fill(cut, "Something broke", "Text");

        cut.Find("form").Submit();

        Assert.Contains("To: support@example.com", cut.Find("textarea.base-issue-fulltext").TextContent);
    }

    [Fact]
    public void CopyDetailsCopiesTheCompleteTextAndConfirms()
    {
        var cut = RenderDialog();
        Fill(cut, "Something broke", "Text");

        cut.FindAll("fluent-button").Single(button => button.TextContent.Contains("Copy details")).Click();

        Browser.Received(1).CopyTextAsync(Arg.Is<string>(text => text.Contains("Subject: [demo] Something broke")));
        Assert.Contains("copied to the clipboard", cut.Markup);
    }

    [Fact]
    public void ALongDescriptionShowsTheShortenedNotice()
    {
        Options.MaxUrlLength = 400;
        Diagnostics.RecordError("Test", new string('e', 250));
        var cut = RenderDialog();

        Fill(cut, "Something broke", "Text");

        Assert.NotEmpty(cut.FindAll(".base-issue-notice"));
    }

    [Fact]
    public void PrefilledValuesAreShown()
    {
        var cut = RenderDialog(new IssueReportDialogContent { Summary = "Prefilled", Description = "Body" });

        Assert.Equal("Prefilled", cut.Find("fluent-text-field").GetAttribute("value"));
    }
}

public sealed class IssueReportRegistrationTests
{
    private static ServiceProvider Build(Action<IssueReportOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        services.AddSingleton(Substitute.For<IDialogService>());
        services.AddBlazorBaseIssueReporting(options =>
        {
            options.Recipient = "support@example.com";
            configure?.Invoke(options);
        });

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TheRegistrationAddsTheBugHeaderActionThatOpensTheDialog()
    {
        using var english = new EnglishCulture();
        using var provider = Build();
        using var scope = provider.CreateScope();

        var item = Assert.Single(scope.ServiceProvider.GetRequiredService<IHeaderActionService>().Items);

        Assert.Equal(IssueReportServiceCollectionExtensions.HeaderActionId, item.Id);
        Assert.Equal("Report an issue", item.Title);
        Assert.True(item.IsVisible!(scope.ServiceProvider));
        await item.OnInvoke(scope.ServiceProvider);
        await scope.ServiceProvider.GetRequiredService<IDialogService>().Received(1).ShowDialogAsync<BaseIssueReportDialog>(Arg.Any<IssueReportDialogContent>(), Arg.Any<DialogParameters>());
    }

    [Fact]
    public void TheHeaderActionCanBeTurnedOffOrHiddenWithoutARecipient()
    {
        using var off = Build(options => options.RegisterHeaderAction = false);
        using var offScope = off.CreateScope();
        Assert.False(Assert.Single(offScope.ServiceProvider.GetRequiredService<IHeaderActionService>().Items).IsVisible!(offScope.ServiceProvider));

        using var none = Build(options => options.Recipient = "");
        using var noneScope = none.CreateScope();
        Assert.False(Assert.Single(noneScope.ServiceProvider.GetRequiredService<IHeaderActionService>().Items).IsVisible!(noneScope.ServiceProvider));
    }

    [Fact]
    public void RegisteringTwiceKeepsASingleHeaderActionAndMergesTheConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        services.AddSingleton(Substitute.For<IDialogService>());
        services.AddBlazorBaseIssueReporting(options => options.Recipient = "a@example.com");
        services.AddBlazorBaseIssueReporting(options => options.SiteName = "demo");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Single(scope.ServiceProvider.GetRequiredService<IHeaderActionService>().Items);
        var options = scope.ServiceProvider.GetRequiredService<IOptions<IssueReportOptions>>().Value;
        Assert.Equal("a@example.com", options.Recipient);
        Assert.Equal("demo", options.SiteName);
    }
}

public sealed class IssueReportLocalizationTests
{
    [Theory]
    [InlineData(typeof(BaseIssueReportDialog))]
    [InlineData(typeof(BaseHeaderActions))]
    public void GermanResources_CoverEveryEnglishKey(Type component)
    {
        var english = Read(component, CultureInfo.InvariantCulture);
        var german = Read(component, new CultureInfo("de"));

        Assert.NotEmpty(english);
        Assert.Empty(english.Keys.Except(german.Keys));
        Assert.Empty(german.Keys.Except(english.Keys));
        Assert.All(german, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
    }

    private static Dictionary<string, string> Read(Type component, CultureInfo culture)
    {
        var assembly = component.Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith($".{component.Name}.resources", StringComparison.Ordinal));
        var manager = new ResourceManager(resourceName[..^".resources".Length], assembly);
        var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!;

        return set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }
}
