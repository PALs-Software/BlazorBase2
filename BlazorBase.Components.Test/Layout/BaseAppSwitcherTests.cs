using System.Globalization;
using BlazorBase.Components.Layout.AppSwitcher;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace BlazorBase.Components.Test.Layout;

public sealed class BaseAppSwitcherTests : ComponentsBunitTestContextBase
{
    private const string ModulePath = "./_content/BlazorBase.Components/js/appSwitcher.js";

    private readonly BunitJSModuleInterop Module;

    public BaseAppSwitcherTests()
    {
        Services.AddLocalization();
        Module = JSInterop.SetupModule(ModulePath);
        Module.SetupVoid("attach", _ => true).SetVoidResult();
        Module.SetupVoid("detach").SetVoidResult();
        Module.SetupVoid("focusInitial", _ => true).SetVoidResult();
    }

    private static IReadOnlyList<AppLinkGroup> SampleGroups() =>
    [
        new()
        {
            Key = "cosmos",
            Label = "My Cosmos",
            Links =
            [
                new() { Key = "my", Label = "my", Href = "https://my.example/", IconSrc = "icons/my.svg" },
                new() { Key = "dev", Label = "dev", Href = "https://dev.example/", IconSrc = "icons/dev.svg", Description = "Developer portal" },
            ],
        },
        new()
        {
            Key = "service",
            Label = "Customer Service",
            Links = [new() { Key = "helpdesk", Label = "Helpdesk", Href = "https://help.example/", OpenInNewTab = false }],
        },
        new()
        {
            Key = "internal",
            Label = "Internal Docs",
            RequiredRole = "Employee",
            Links =
            [
                new() { Key = "docs", Label = "Internal Docs", Href = "https://docs.example/" },
                new() { Key = "tools", Label = "Tool Catalogue", Href = "https://tools.example/", RequiredRole = "Admin" },
            ],
        },
    ];

    private IRenderedComponent<BaseAppSwitcher> RenderSwitcher(
        IReadOnlyList<AppLinkGroup>? groups = null,
        string? currentKey = "dev",
        Action<ComponentParameterCollectionBuilder<BaseAppSwitcher>>? configure = null)
    {
        return Render<BaseAppSwitcher>(parameters =>
        {
            parameters.Add(p => p.Groups, groups ?? SampleGroups());
            parameters.Add(p => p.CurrentKey, currentKey);
            configure?.Invoke(parameters);
        });
    }

    private BunitAuthorizationContext Authorize(params string[] roles)
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("user");
        if (roles.Length > 0)
            auth.SetRoles(roles);

        return auth;
    }

    private static void OpenPopover(IRenderedComponent<BaseAppSwitcher> cut) => cut.Find("button.base-app-switcher-chip").Click();

    [Fact]
    public void Closed_RendersTheChipWithTheCurrentAppAndNoPopover()
    {
        Authorize("Employee");

        var cut = RenderSwitcher();

        var chip = cut.Find("button.base-app-switcher-chip");
        Assert.Equal("dev", cut.Find(".base-app-switcher-chip-label").TextContent);
        Assert.Equal("icons/dev.svg", cut.Find("img.base-app-switcher-chip-icon").GetAttribute("src"));
        Assert.Equal("false", chip.GetAttribute("aria-expanded"));
        Assert.Equal("dialog", chip.GetAttribute("aria-haspopup"));
        Assert.Empty(cut.FindAll(".base-app-switcher-popover"));
        Assert.Empty(cut.FindAll(".base-app-switcher-item"));
        Assert.Null(chip.GetAttribute("aria-controls"));
    }

    [Fact]
    public void CurrentLabelAndIcon_OverrideTheValuesOfTheCurrentLink()
    {
        Authorize();

        var cut = RenderSwitcher(configure: p => p.Add(x => x.CurrentLabel, "Custom").Add(x => x.CurrentIconSrc, "custom.svg"));

        Assert.Equal("Custom", cut.Find(".base-app-switcher-chip-label").TextContent);
        Assert.Equal("custom.svg", cut.Find("img.base-app-switcher-chip-icon").GetAttribute("src"));
    }

    [Fact]
    public void ChipWithoutAnIcon_ShowsAGlyph()
    {
        Authorize();

        var cut = RenderSwitcher(currentKey: "helpdesk");

        Assert.Empty(cut.FindAll("img.base-app-switcher-chip-icon"));
        Assert.Single(cut.FindAll("button.base-app-switcher-chip svg"), svg => svg.ClassList.Contains("base-app-switcher-chip-glyph"));
    }

    [Fact]
    public void ChipAccessibleName_NamesTheCurrentAppUnlessOverridden()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        try
        {
            Authorize();

            var cut = RenderSwitcher();
            Assert.Equal("dev, switch website", cut.Find("button.base-app-switcher-chip").GetAttribute("aria-label"));

            cut.Render(parameters => parameters.Add(p => p.AriaLabel, "Apps"));
            Assert.Equal("Apps", cut.Find("button.base-app-switcher-chip").GetAttribute("aria-label"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void ClickingTheChip_OpensADialogWithTheGroupsAndStartsTheJsHandling()
    {
        Authorize("Employee", "Admin");

        var cut = RenderSwitcher();
        OpenPopover(cut);

        var popover = cut.Find(".base-app-switcher-popover");
        Assert.Equal("dialog", popover.GetAttribute("role"));
        Assert.Equal("true", cut.Find("button.base-app-switcher-chip").GetAttribute("aria-expanded"));
        Assert.Equal(popover.Id, cut.Find("button.base-app-switcher-chip").GetAttribute("aria-controls"));
        Assert.Equal(["My Cosmos", "Customer Service", "Internal Docs"], cut.FindAll(".base-app-switcher-group-title").Select(title => title.TextContent));
        Assert.Equal(5, cut.FindAll("a.base-app-switcher-item").Count);
        Module.VerifyInvoke("attach");
        Module.VerifyInvoke("focusInitial");
    }

    [Fact]
    public void ClickingTheChipAgain_ClosesThePopoverAndStopsTheJsHandling()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);
        OpenPopover(cut);

        Assert.Empty(cut.FindAll(".base-app-switcher-popover"));
        Assert.Equal("false", cut.Find("button.base-app-switcher-chip").GetAttribute("aria-expanded"));
        Module.VerifyInvoke("detach");
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void Escape_ClosesThePopoverAndReturnsTheFocusToTheChip()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        cut.Find(".base-app-switcher").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".base-app-switcher-popover"));
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void OtherKeys_LeaveThePopoverOpen()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        cut.Find(".base-app-switcher").KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.Single(cut.FindAll(".base-app-switcher-popover"));
    }

    [Fact]
    public async Task DismissFromTheBrowser_ClosesThePopoverWithoutStealingTheFocus()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        await cut.InvokeAsync(() => cut.Instance.OnDismissRequested(false));

        Assert.Empty(cut.FindAll(".base-app-switcher-popover"));
        Assert.DoesNotContain(JSInterop.Invocations, invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase) && invocation.Identifier != "focusInitial");
    }

    [Fact]
    public async Task DismissFromTheBrowserWithFocusRestore_ReturnsTheFocusToTheChip()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        await cut.InvokeAsync(() => cut.Instance.OnDismissRequested(true));

        Assert.Empty(cut.FindAll(".base-app-switcher-popover"));
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void ClickingTheBackdrop_ClosesThePopover()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        cut.Find(".base-app-switcher-backdrop").Click();

        Assert.Empty(cut.FindAll(".base-app-switcher-popover"));
    }

    [Fact]
    public void ChoosingALink_ClosesThePopover()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        cut.Find("a.base-app-switcher-item").Click();

        Assert.Empty(cut.FindAll(".base-app-switcher-popover"));
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void Links_OpenInANewTabWithNoopenerUnlessOptedOut()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        var my = cut.FindAll("a.base-app-switcher-item").Single(a => a.GetAttribute("href") == "https://my.example/");
        Assert.Equal("_blank", my.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", my.GetAttribute("rel"));

        var helpdesk = cut.FindAll("a.base-app-switcher-item").Single(a => a.GetAttribute("href") == "https://help.example/");
        Assert.Null(helpdesk.GetAttribute("target"));
        Assert.Null(helpdesk.GetAttribute("rel"));
    }

    [Fact]
    public void CurrentApp_IsMarkedWithAriaCurrentTheCurrentClassAndACheck()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        var current = cut.FindAll("a.base-app-switcher-item[aria-current]");
        var link = Assert.Single(current);
        Assert.Equal("https://dev.example/", link.GetAttribute("href"));
        Assert.Contains("current", link.ClassList);
        Assert.Single(link.QuerySelectorAll(".base-app-switcher-check"));
        Assert.Single(cut.FindAll(".base-app-switcher-check"));
    }

    [Fact]
    public void LinkWithoutIcon_GetsAGlyphAndADescriptionIsShown()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        var helpdesk = cut.FindAll("a.base-app-switcher-item").Single(a => a.GetAttribute("href") == "https://help.example/");
        Assert.Empty(helpdesk.QuerySelectorAll("img"));
        Assert.Single(helpdesk.QuerySelectorAll("svg"));
        Assert.Equal("Developer portal", cut.Find(".base-app-switcher-desc").TextContent);
    }

    [Fact]
    public void GroupWithAMissingRole_IsHidden()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        Assert.Equal(["My Cosmos", "Customer Service"], cut.FindAll(".base-app-switcher-group-title").Select(title => title.TextContent));
        Assert.DoesNotContain("docs.example", cut.Markup);
    }

    [Fact]
    public void GroupWithTheRole_IsShownAndLinkLevelRolesAreStillApplied()
    {
        Authorize("Employee");

        var cut = RenderSwitcher();
        OpenPopover(cut);

        Assert.Contains("https://docs.example/", cut.Markup);
        Assert.DoesNotContain("https://tools.example/", cut.Markup);
    }

    [Fact]
    public void CommaSeparatedRoles_NeedOnlyOneMatch()
    {
        Authorize("Admin");
        var groups = new List<AppLinkGroup>
        {
            new() { Key = "g", Label = "G", RequiredRole = "Employee, Admin", Links = [new() { Key = "a", Label = "A", Href = "https://a.example/" }] },
        };

        var cut = RenderSwitcher(groups, currentKey: null);
        OpenPopover(cut);

        Assert.Single(cut.FindAll("a.base-app-switcher-item"));
    }

    [Fact]
    public void GroupWithoutVisibleLinks_IsDropped()
    {
        Authorize("Employee");
        var groups = new List<AppLinkGroup>
        {
            new() { Key = "visible", Label = "Visible", Links = [new() { Key = "a", Label = "A", Href = "https://a.example/" }] },
            new() { Key = "empty", Label = "Empty", Links = [new() { Key = "b", Label = "B", Href = "https://b.example/", RequiredRole = "Admin" }] },
        };

        var cut = RenderSwitcher(groups, currentKey: "a");
        OpenPopover(cut);

        Assert.Equal(["Visible"], cut.FindAll(".base-app-switcher-group-title").Select(title => title.TextContent));
    }

    [Fact]
    public void WithoutAnAuthenticatedUser_RoleGatedContentIsHidden()
    {
        var cut = RenderSwitcher();
        OpenPopover(cut);

        Assert.DoesNotContain("docs.example", cut.Markup);
        Assert.Equal(["My Cosmos", "Customer Service"], cut.FindAll(".base-app-switcher-group-title").Select(title => title.TextContent));
    }

    [Fact]
    public void WithoutVisibleLinks_TheChipIsAPlainLabelWithoutAPopover()
    {
        Authorize();
        var groups = new List<AppLinkGroup>
        {
            new() { Key = "internal", Label = "Internal", RequiredRole = "Employee", Links = [new() { Key = "docs", Label = "Docs", Href = "https://docs.example/" }] },
        };

        var cut = RenderSwitcher(groups, currentKey: null, configure: p => p.Add(x => x.CurrentLabel, "Portal"));

        Assert.Empty(cut.FindAll("button"));
        Assert.Equal("Portal", cut.Find(".base-app-switcher-chip.static").TextContent.Trim());
    }

    [Fact]
    public void WithoutLinksAndLabel_RendersNothing()
    {
        Authorize();

        var cut = RenderSwitcher(groups: [], currentKey: null);

        Assert.Empty(cut.FindAll(".base-app-switcher"));
    }

    [Fact]
    public void FourGroups_AreSplitIntoTwoBalancedColumnsInOrder()
    {
        Authorize("Employee", "Admin");
        var groups = new List<AppLinkGroup>
        {
            Group("one", 4),
            Group("two", 2),
            Group("three", 3),
            Group("four", 4),
        };

        var cut = RenderSwitcher(groups, currentKey: null);
        OpenPopover(cut);

        var columns = cut.FindAll(".base-app-switcher-column");
        Assert.Equal(2, columns.Count);
        Assert.Equal(["one", "two"], columns[0].QuerySelectorAll(".base-app-switcher-group-title").Select(title => title.TextContent));
        Assert.Equal(["three", "four"], columns[1].QuerySelectorAll(".base-app-switcher-group-title").Select(title => title.TextContent));
    }

    [Fact]
    public void SingleGroup_UsesOneColumn()
    {
        Authorize();

        var cut = RenderSwitcher([Group("only", 3)], currentKey: null);
        OpenPopover(cut);

        Assert.Single(cut.FindAll(".base-app-switcher-column"));
    }

    [Fact]
    public void ChangingTheGroupsWhileOpen_ClosesWhenNothingIsVisibleAnymore()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        cut.Render(parameters => parameters.Add(p => p.Groups, []));

        Assert.Empty(cut.FindAll(".base-app-switcher-popover"));
    }

    [Fact]
    public async Task Disposing_StopsTheJsHandling()
    {
        Authorize();

        var cut = RenderSwitcher();
        OpenPopover(cut);

        await cut.Instance.DisposeAsync();

        Module.VerifyInvoke("detach");
    }

    [Theory]
    [InlineData("en", "Switch website")]
    [InlineData("de", "Website wechseln")]
    public void LocalizedStrings_ExistInEnglishAndGerman(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        try
        {
            var localizer = Services.GetRequiredService<IStringLocalizer<BaseAppSwitcher>>();

            Assert.Equal(expected, localizer["SwitchWebsite"].Value);
            Assert.False(localizer["SwitchWebsiteFrom", "x"].ResourceNotFound);
            Assert.False(localizer["PopoverLabel"].ResourceNotFound);
            Assert.Contains("x", localizer["SwitchWebsiteFrom", "x"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static AppLinkGroup Group(string key, int linkCount) => new()
    {
        Key = key,
        Label = key,
        Links = Enumerable.Range(0, linkCount)
            .Select(index => new AppLink { Key = $"{key}-{index}", Label = $"{key} {index}", Href = $"https://{key}{index}.example/" })
            .ToList(),
    };
}
