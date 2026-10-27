using System.Globalization;
using BlazorBase.Components.CommandPalette;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Components.Test.CommandPalette;

public sealed class BaseCommandPaletteTests : ComponentsBunitTestContextBase
{
    private const string ModulePath = "./_content/BlazorBase.Components/js/commandPalette.js";

    private readonly BunitJSModuleInterop Module;
    private readonly List<string> Executed = [];

    public BaseCommandPaletteTests()
    {
        Services.AddLocalization();
        Services.AddBlazorBaseCommandPalette(options =>
        {
            options.SearchDebounce = TimeSpan.Zero;
            options.LoadingIndicatorDelay = TimeSpan.FromMilliseconds(80);
        });

        Module = JSInterop.SetupModule(ModulePath);
        Module.SetupVoid("registerShortcut", _ => true).SetVoidResult();
        Module.SetupVoid("unregisterShortcut").SetVoidResult();
        Module.SetupVoid("activate", _ => true).SetVoidResult();
        Module.SetupVoid("deactivate").SetVoidResult();
        Module.SetupVoid("scrollToActive", _ => true).SetVoidResult();
        Module.SetupVoid("writeRecent", _ => true).SetVoidResult();
        Module.SetupVoid("openExternal", _ => true).SetVoidResult();
    }

    private ICommandPaletteService PaletteService => Services.GetRequiredService<ICommandPaletteService>();

    private static ICommandPaletteProvider Provider(params CommandPaletteEntry[] entries) => new StaticCommandPaletteProvider(entries);

    private static CommandPaletteEntry Page(string id, string title, string? subtitle = null, string? role = null, string group = "Pages", int groupOrder = 100) =>
        new() { Id = id, Title = title, Subtitle = subtitle, Group = group, GroupOrder = groupOrder, Href = "/" + id, RequiredRole = role };

    private CommandPaletteEntry Action(string id, string title, Func<Task>? execute = null) =>
        new()
        {
            Id = id,
            Title = title,
            Group = "Actions",
            GroupOrder = 200,
            Execute = execute ?? (() =>
            {
                Executed.Add(id);
                return Task.CompletedTask;
            }),
        };

    private IRenderedComponent<BaseCommandPalette> RenderPalette(params ICommandPaletteProvider[] providers)
    {
        AddAuthorization().SetAuthorized("user");
        return Render<BaseCommandPalette>(parameters => parameters.Add(p => p.Providers, providers));
    }

    private async Task<IRenderedComponent<BaseCommandPalette>> OpenAsync(string? query = null, params ICommandPaletteProvider[] providers)
    {
        var cut = RenderPalette(providers);
        await cut.InvokeAsync(() => PaletteService.OpenAsync(query));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".base-command-palette")));
        return cut;
    }

    private static List<string> RowTitles(IRenderedComponent<BaseCommandPalette> cut) =>
        cut.FindAll(".base-command-palette-row .base-command-palette-title").Select(title => title.TextContent).ToList();

    private static void Type(IRenderedComponent<BaseCommandPalette> cut, string text) => cut.Find("input").Input(text);

    private static void PressKey(IRenderedComponent<BaseCommandPalette> cut, string key, bool ctrl = false) =>
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = key, CtrlKey = ctrl });

    [Fact]
    public void Closed_RendersNothingAndRegistersTheShortcut()
    {
        var cut = RenderPalette(Provider(Page("a", "Alpha")));

        Assert.Empty(cut.FindAll(".base-command-palette"));
        Module.VerifyInvoke("registerShortcut");
    }

    [Fact]
    public async Task Open_ShowsAModalComboboxOverAListbox()
    {
        using var english = new EnglishCulture();
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha"), Page("b", "Beta")));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[role=option]").Count));
        var dialog = cut.Find("[role=dialog]");
        var input = cut.Find("input");
        var listbox = cut.Find("[role=listbox]");
        var options = cut.FindAll("[role=option]");

        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        Assert.Equal("Search", dialog.GetAttribute("aria-label"));
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("true", input.GetAttribute("aria-expanded"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
        Assert.Equal(listbox.Id, input.GetAttribute("aria-controls"));
        Assert.Equal(options[0].Id, input.GetAttribute("aria-activedescendant"));
        Assert.Equal("true", options[0].GetAttribute("aria-selected"));
        Assert.Equal("false", options[1].GetAttribute("aria-selected"));
        Assert.Equal("Search pages, actions and apps…", input.GetAttribute("placeholder"));
        Module.VerifyInvoke("activate");
    }

    [Fact]
    public async Task Groups_AreLabelledAndOrdered()
    {
        var cut = await OpenAsync(null, Provider(Action("x", "Run it"), Page("a", "Alpha")));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[role=group]").Count));
        var groups = cut.FindAll("[role=group]");

        Assert.Equal("Pages", cut.Find($"#{groups[0].GetAttribute("aria-labelledby")}").TextContent);
        Assert.Equal("Actions", cut.Find($"#{groups[1].GetAttribute("aria-labelledby")}").TextContent);
    }

    [Fact]
    public async Task Typing_FiltersRanksAndHighlightsTheMatch()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Seats"), Page("b", "Monthly Budgets", "GitHub Copilot"), Page("c", "Budgets")));

        Type(cut, "bud");

        cut.WaitForAssertion(() => Assert.Equal(["Budgets", "Monthly Budgets"], RowTitles(cut)));
        Assert.Equal("Bud", cut.Find(".base-command-palette-row mark").TextContent);
    }

    [Fact]
    public async Task Texts_AreRenderedAsTextNeverAsHtml()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "<b>Bold</b> budget", "<img src=x onerror=alert(1)>")));

        Type(cut, "bud");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=option]")));
        Assert.Empty(cut.FindAll(".base-command-palette-row b"));
        Assert.Empty(cut.FindAll(".base-command-palette-row img[onerror]"));
        Assert.Contains("<b>Bold</b> budget", cut.Find(".base-command-palette-title").TextContent);
    }

    [Fact]
    public async Task NoMatch_ShowsTheEmptyState()
    {
        using var english = new EnglishCulture();
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha")));

        Type(cut, "zzz");

        cut.WaitForAssertion(() => Assert.Equal("No results", cut.Find(".base-command-palette-empty").TextContent.Trim()));
        Assert.Empty(cut.FindAll("[role=option]"));
        Assert.Null(cut.Find("input").GetAttribute("aria-activedescendant"));
    }

    [Fact]
    public async Task ArrowKeys_MoveTheSelectionAndWrapAround()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha"), Page("b", "Beta"), Page("c", "Gamma")));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[role=option]").Count));

        string Selected() => cut.Find("[role=option][aria-selected=true] .base-command-palette-title").TextContent;

        PressKey(cut, "ArrowDown");
        Assert.Equal("Beta", Selected());
        PressKey(cut, "ArrowDown");
        PressKey(cut, "ArrowDown");
        Assert.Equal("Alpha", Selected());
        PressKey(cut, "ArrowUp");
        Assert.Equal("Gamma", Selected());
        Assert.Equal(cut.Find("[role=option][aria-selected=true]").Id, cut.Find("input").GetAttribute("aria-activedescendant"));
        Assert.NotEmpty(Module.Invocations["scrollToActive"]);
    }

    [Fact]
    public async Task HomeAndEnd_JumpWhileTheInputIsEmptyOrWithCtrl()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha"), Page("b", "Beta"), Page("c", "Gamma")));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[role=option]").Count));

        string Selected() => cut.Find("[role=option][aria-selected=true] .base-command-palette-title").TextContent;

        PressKey(cut, "End");
        Assert.Equal("Gamma", Selected());
        PressKey(cut, "Home");
        Assert.Equal("Alpha", Selected());

        Type(cut, "a");
        cut.WaitForAssertion(() => Assert.True(cut.FindAll("[role=option]").Count >= 2));
        PressKey(cut, "End");
        Assert.Equal("Alpha", Selected());
        PressKey(cut, "End", ctrl: true);
        Assert.NotEqual("Alpha", Selected());
    }

    [Fact]
    public async Task Enter_NavigatesToThePageClosesAndRemembersTheEntry()
    {
        var cut = await OpenAsync(null, Provider(Page("budgets", "Budgets"), Page("seats", "Seats")));
        Type(cut, "bud");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=option]")));

        PressKey(cut, "Enter");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-command-palette")));
        Assert.EndsWith("/budgets", Services.GetRequiredService<NavigationManager>().Uri);
        Assert.False(PaletteService.IsOpen);
        Module.VerifyInvoke("writeRecent");
        Module.VerifyInvoke("deactivate");
    }

    [Fact]
    public async Task Click_RunsTheEntry()
    {
        var cut = await OpenAsync(null, Provider(Action("flash", "Flash it"), Page("a", "Alpha")));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[role=option]").Count));

        cut.FindAll("[role=option]").Single(option => option.TextContent.Contains("Flash it")).Click();

        cut.WaitForAssertion(() => Assert.Equal(["flash"], Executed));
        Assert.False(PaletteService.IsOpen);
    }

    [Fact]
    public async Task AnActionThatThrows_IsCaughtAndThePaletteStillCloses()
    {
        var cut = await OpenAsync(null, Provider(Action("bad", "Bad action", () => throw new InvalidOperationException("boom"))));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=option]")));

        PressKey(cut, "Enter");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-command-palette")));
        Assert.False(PaletteService.IsOpen);
    }

    [Fact]
    public async Task Escape_ClosesAndReturnsTheFocus()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha")));

        PressKey(cut, "Escape");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-command-palette")));
        Assert.False(PaletteService.IsOpen);
        Module.VerifyInvoke("deactivate");
    }

    [Fact]
    public async Task OnEscape_FromTheBrowser_ClosesThePaletteWhereverTheFocusIs()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha")));

        await cut.InvokeAsync(() => cut.Instance.OnEscape());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-command-palette")));
        Assert.False(PaletteService.IsOpen);
        Module.VerifyInvoke("deactivate");
    }

    [Fact]
    public async Task OnEscape_WhenAlreadyClosed_DoesNothing()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha")));
        await cut.InvokeAsync(() => cut.Instance.OnEscape());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-command-palette")));

        await cut.InvokeAsync(() => cut.Instance.OnEscape());

        Assert.Empty(cut.FindAll(".base-command-palette"));
        Assert.False(PaletteService.IsOpen);
    }

    [Fact]
    public async Task Open_PassesTheComponentReferenceToTheModuleForTheEscapeCallback()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha")));

        var activation = Module.Invocations["activate"].Single();

        Assert.Equal(3, activation.Arguments.Count);
        Assert.IsType<Microsoft.JSInterop.DotNetObjectReference<BaseCommandPalette>>(activation.Arguments[2]);
        Assert.NotNull(cut.Find(".base-command-palette"));
    }

    [Fact]
    public async Task ThePanel_CanTakeTheFocusProgrammaticallyButIsNotATabStop()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha")));

        Assert.Equal("-1", cut.Find(".base-command-palette").GetAttribute("tabindex"));
    }

    [Fact]
    public async Task ClickingTheBackdrop_Closes()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha")));

        cut.Find(".base-command-palette-overlay").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-command-palette")));
    }

    [Fact]
    public async Task ExternalEntries_OpenInANewTabThroughTheModule()
    {
        var external = new CommandPaletteEntry { Id = "docs", Title = "Docs site", Group = "Apps", Href = "https://docs.example/start" };
        var cut = await OpenAsync(null, Provider(external));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=option]")));

        PressKey(cut, "Enter");

        cut.WaitForAssertion(() => Module.VerifyInvoke("openExternal"));
        Assert.Equal("https://docs.example/start", Module.Invocations["openExternal"].Single().Arguments[0]);
        Assert.DoesNotContain("docs.example", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task InternalEntriesWithOpenInNewTab_OpenTheAbsoluteAddress()
    {
        var entry = Page("budgets", "Budgets") with { OpenInNewTab = true };
        var cut = await OpenAsync(null, Provider(entry));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=option]")));

        PressKey(cut, "Enter");

        cut.WaitForAssertion(() => Module.VerifyInvoke("openExternal"));
        Assert.EndsWith("/budgets", (string)Module.Invocations["openExternal"].Single().Arguments[0]!);
    }

    [Fact]
    public async Task EntriesWithADisallowedScheme_AreNeverOpened()
    {
        var entry = new CommandPaletteEntry { Id = "evil", Title = "Evil link", Group = "Apps", Href = "javascript:alert(1)" };
        var cut = await OpenAsync(null, Provider(entry));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=option]")));
        var before = Services.GetRequiredService<NavigationManager>().Uri;

        PressKey(cut, "Enter");

        cut.WaitForAssertion(() => Assert.False(PaletteService.IsOpen));
        Module.VerifyNotInvoke("openExternal");
        Assert.Equal(before, Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task EntriesTheUserMayNotUse_AreNotShown()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Open budget"), Page("b", "Secret budget", role: "Admin")));

        Type(cut, "budget");

        cut.WaitForAssertion(() => Assert.Equal(["Open budget"], RowTitles(cut)));
    }

    [Fact]
    public async Task Providers_AreCombinedFromServicesAndTheParameter()
    {
        Services.AddCommandPaletteProvider([Page("registered", "Registered page")]);
        var cut = await OpenAsync(null, Provider(Page("param", "Parameter page")));

        cut.WaitForAssertion(() => Assert.Equal(["Parameter page", "Registered page"], RowTitles(cut).Order().ToList()));
    }

    [Fact]
    public async Task AFailingProvider_LeavesTheOthersVisible()
    {
        var broken = new DelegateCommandPaletteProvider((_, _) => throw new InvalidOperationException("boom"));
        var cut = await OpenAsync(null, broken, Provider(Page("a", "Alpha")));

        cut.WaitForAssertion(() => Assert.Equal(["Alpha"], RowTitles(cut)));
    }

    [Fact]
    public async Task TheService_OpensWithAnInitialQuery()
    {
        var cut = await OpenAsync("bud", Provider(Page("a", "Budgets"), Page("b", "Seats")));

        cut.WaitForAssertion(() => Assert.Equal(["Budgets"], RowTitles(cut)));
        Assert.Equal("bud", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task RecentEntries_AreListedFirstUnderRecentWhenTheQueryIsEmpty()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Module.Setup<string[]>("readRecent", _ => true).SetResult(["page:/b"]);
        using var english = new EnglishCulture();
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha"), new CommandPaletteEntry { Id = "page:/b", Title = "Beta", Group = "Pages", Href = "/b" }));

        cut.WaitForAssertion(() => Assert.Equal(["Beta", "Alpha"], RowTitles(cut)));
        Assert.Equal("Recent", cut.FindAll(".base-command-palette-group-title")[0].TextContent);
    }

    [Fact]
    public async Task OnShortcut_TogglesThePalette()
    {
        var cut = RenderPalette(Provider(Page("a", "Alpha")));

        await cut.InvokeAsync(() => cut.Instance.OnShortcut(true));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-command-palette")));

        await cut.InvokeAsync(() => cut.Instance.OnShortcut(true));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-command-palette")));

        await cut.InvokeAsync(() => cut.Instance.OnShortcut(false));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-command-palette")));
        await cut.InvokeAsync(() => cut.Instance.OnShortcut(false));
        Assert.True(PaletteService.IsOpen);
    }

    [Fact]
    public async Task ASlowEarlierSearch_NeverOverwritesTheNewerResults()
    {
        var provider = new DelegateCommandPaletteProvider(async (request, token) =>
        {
            if (request.Query == "a")
            {
                await Task.Delay(400, token);
                return [Page("slow", "Slow answer")];
            }

            return [Page("fast", "Fast answer")];
        });
        var cut = await OpenAsync(null, provider);

        Type(cut, "a");
        Type(cut, "an");

        cut.WaitForAssertion(() => Assert.Equal(["Fast answer"], RowTitles(cut)));
        await Task.Delay(600);
        Assert.Equal(["Fast answer"], RowTitles(cut));
    }

    [Fact]
    public async Task ASlowSearch_ShowsALoadingIndicatorAfterTheDelay()
    {
        var provider = new DelegateCommandPaletteProvider(async (_, token) =>
        {
            await Task.Delay(400, token);
            return [Page("a", "Alpha")];
        });
        var cut = RenderPalette(provider);

        await cut.InvokeAsync(() => PaletteService.OpenAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-command-palette-loading")), TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() => Assert.Equal(["Alpha"], RowTitles(cut)), TimeSpan.FromSeconds(3));
        Assert.Empty(cut.FindAll(".base-command-palette-loading"));
    }

    [Fact]
    public async Task AFastSearch_NeverShowsALoadingIndicator()
    {
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha")));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=option]")));
        await Task.Delay(200);

        Assert.Empty(cut.FindAll(".base-command-palette-loading"));
    }

    [Fact]
    public async Task TheStatusRegion_AnnouncesTheNumberOfResults()
    {
        using var english = new EnglishCulture();
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha"), Page("b", "Beta")));

        cut.WaitForAssertion(() => Assert.Equal("2 results", cut.Find("[role=status]").TextContent));
        Assert.Equal("polite", cut.Find("[role=status]").GetAttribute("aria-live"));
    }

    [Fact]
    public async Task ShowsGermanTextsInAGermanInterface()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("de");

        try
        {
            var cut = await OpenAsync(null, Provider(Page("a", "Alpha")));
            Type(cut, "zzz");

            Assert.Equal("Seiten, Aktionen und Apps suchen…", cut.Find("input").GetAttribute("placeholder"));
            cut.WaitForAssertion(() => Assert.Equal("Keine Ergebnisse", cut.Find(".base-command-palette-empty").TextContent.Trim()));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task EntriesWithoutAHint_GetADefaultKindLabel()
    {
        using var english = new EnglishCulture();
        var external = new CommandPaletteEntry { Id = "docs", Title = "Docs", Group = "Apps", Href = "https://docs.example/" };
        var cut = await OpenAsync(null, Provider(Page("a", "Alpha"), Action("x", "Run"), external));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[role=option]").Count));

        var hints = cut.FindAll(".base-command-palette-hint").Select(hint => hint.TextContent).ToList();
        Assert.Equal(["Link", "Page", "Action"], hints);
    }
}
