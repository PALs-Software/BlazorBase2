using BlazorBase.Components.CommandPalette;
using BlazorBase.Components.Layout;
using BlazorBase.Components.Layout.AppSwitcher;
using BlazorBase.Components.Layout.Navigation;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.Components.Test.Layout;

public sealed class BaseLayoutCommandPaletteTests : ComponentsBunitTestContextBase
{
    private readonly BunitAuthorizationContext Authorization;

    public BaseLayoutCommandPaletteTests()
    {
        Services.AddLocalization();
        Services.AddSingleton(Substitute.For<IThemeService>());
        Services.AddSingleton(Substitute.For<ILanguageService>());
        Services.AddBlazorBaseCommandPalette(options => options.SearchDebounce = TimeSpan.Zero);
        Authorization = AddAuthorization();
        Authorization.SetAuthorized("user");
    }

    private static IReadOnlyList<NavigationItem> Items() =>
    [
        new() { Href = "/", Label = "Home", MatchAll = true },
        new() { Label = "Copilot", Children = [new() { Href = "/budgets", Label = "Budgets" }] },
        new() { Href = "/admin", Label = "Admin area", RequiredRole = "Admin" },
    ];

    private static IReadOnlyList<AppLinkGroup> Links() =>
    [
        new()
        {
            Key = "cosmos",
            Label = "My Cosmos",
            Links =
            [
                new() { Key = "dev", Label = "dev", Href = "https://dev.example/", IconSrc = "dev.svg" },
                new() { Key = "my", Label = "my", Href = "https://my.example/" },
            ],
        },
    ];

    private IRenderedComponent<BaseLayout> RenderLayout(Action<ComponentParameterCollectionBuilder<BaseLayout>>? configure = null) =>
        Render<BaseLayout>(parameters =>
        {
            parameters.Add(p => p.AppTitle, "Shell");
            configure?.Invoke(parameters);
        });

    private static List<string> PaletteRows(IRenderedComponent<BaseLayout> cut) =>
        cut.FindAll(".base-command-palette-row .base-command-palette-title").Select(title => title.TextContent).ToList();

    private static async Task OpenPaletteAsync(IRenderedComponent<BaseLayout> cut)
    {
        await cut.InvokeAsync(() => cut.Services.GetRequiredService<ICommandPaletteService>().OpenAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-command-palette")));
    }

    [Fact]
    public void WithoutShowCommandPalette_NothingChanges()
    {
        var cut = RenderLayout(parameters => parameters.Add(p => p.NavigationItems, Items()));

        Assert.Empty(cut.FindAll(".base-command-search"));
        Assert.Empty(cut.FindAll(".base-command-search-icon"));
        Assert.Empty(cut.FindAll(".base-command-palette"));
        Assert.Contains("brand-centered", cut.Find("header").ClassList);
        Assert.DoesNotContain("has-command-palette", cut.Find("header").ClassList);
        Assert.NotNull(cut.Find(".base-header-center .base-header-title"));
    }

    [Fact]
    public void WithShowCommandPalette_TheHeaderGetsACenteredSearchFieldAndThePhoneButton()
    {
        using var english = new EnglishCulture();

        var cut = RenderLayout(parameters => parameters.Add(p => p.ShowCommandPalette, true));

        var header = cut.Find("header");
        Assert.Contains("has-command-palette", header.ClassList);
        Assert.Contains("brand-start", header.ClassList);

        var field = cut.Find(".base-header-center.base-header-search button.base-command-search");
        Assert.Equal("dialog", field.GetAttribute("aria-haspopup"));
        Assert.Equal("Control+K", field.GetAttribute("aria-keyshortcuts"));
        Assert.Equal("Search pages, actions and apps…", field.QuerySelector(".base-command-search-text")!.TextContent);
        Assert.Equal("Ctrl K", field.QuerySelector("kbd")!.TextContent);

        var phoneButton = cut.Find(".base-header-end fluent-button.base-command-search-icon");
        Assert.Equal("Search", phoneButton.GetAttribute("aria-label"));
        Assert.Equal("dialog", phoneButton.GetAttribute("aria-haspopup"));
    }

    [Fact]
    public void WithShowCommandPalette_TheBrandMovesNextToTheNavigationButton()
    {
        var cut = RenderLayout(parameters => parameters.Add(p => p.ShowCommandPalette, true));

        Assert.NotNull(cut.Find(".base-header-start .base-header-title"));
        Assert.Empty(cut.FindAll(".base-header-center .base-header-title"));
    }

    [Fact]
    public void ThePhoneButton_IsTheFirstControlOfTheHeaderEnd()
    {
        var cut = RenderLayout(parameters => parameters
            .Add(p => p.ShowCommandPalette, true)
            .Add(p => p.ShowHelp, false)
            .Add(p => p.HeaderContent, "<span id=\"custom\">x</span>"));

        var children = cut.Find(".base-header-end").Children;

        Assert.Contains("base-command-search-icon", children[0].ClassList);
        Assert.Equal("custom", children[1].Id);
    }

    [Fact]
    public async Task ClickingTheSearchField_OpensThePalette()
    {
        var cut = RenderLayout(parameters => parameters.Add(p => p.ShowCommandPalette, true));

        cut.Find("button.base-command-search").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-command-palette")));
        Assert.True(Services.GetRequiredService<ICommandPaletteService>().IsOpen);
        await Task.CompletedTask;
    }

    [Fact]
    public void ClickingThePhoneButton_OpensThePalette()
    {
        var cut = RenderLayout(parameters => parameters.Add(p => p.ShowCommandPalette, true));

        cut.Find("fluent-button.base-command-search-icon").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-command-palette")));
    }

    [Fact]
    public async Task ThePalette_OffersTheNavigationItemsAsPagesWithBreadcrumbs()
    {
        var cut = RenderLayout(parameters => parameters
            .Add(p => p.ShowCommandPalette, true)
            .Add(p => p.NavigationItems, Items()));

        await OpenPaletteAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal(["Home", "Budgets"], PaletteRows(cut)));
        Assert.Equal("Copilot", cut.FindAll(".base-command-palette-subtitle").Single().TextContent);
    }

    [Fact]
    public async Task ThePalette_FallsBackToTheBottomNavigationItems()
    {
        var cut = RenderLayout(parameters => parameters
            .Add(p => p.ShowCommandPalette, true)
            .Add(p => p.BottomNavigationItems, Items()));

        await OpenPaletteAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal(["Home", "Budgets"], PaletteRows(cut)));
    }

    [Fact]
    public async Task NavigationItems_TakePrecedenceOverTheBottomNavigationItems()
    {
        IReadOnlyList<NavigationItem> other = [new() { Href = "/other", Label = "Other" }];

        var cut = RenderLayout(parameters => parameters
            .Add(p => p.ShowCommandPalette, true)
            .Add(p => p.NavigationItems, Items())
            .Add(p => p.BottomNavigationItems, other));

        await OpenPaletteAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal(["Home", "Budgets"], PaletteRows(cut)));
    }

    [Fact]
    public async Task ThePalette_OffersRoleProtectedPagesOnlyToUsersInTheRole()
    {
        Authorization.SetRoles("Admin");
        var cut = RenderLayout(parameters => parameters
            .Add(p => p.ShowCommandPalette, true)
            .Add(p => p.NavigationItems, Items()));

        await OpenPaletteAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal(["Home", "Budgets", "Admin area"], PaletteRows(cut)));
    }

    [Fact]
    public async Task ThePalette_OffersTheAppLinksUnderApps()
    {
        var cut = RenderLayout(parameters => parameters
            .Add(p => p.ShowCommandPalette, true)
            .Add(p => p.AppLinks, Links())
            .Add(p => p.NavigationItems, Items()));

        await OpenPaletteAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal(["Home", "Budgets", "dev", "my"], PaletteRows(cut)));
        var groupTitles = cut.FindAll(".base-command-palette-group-title").Select(title => title.TextContent).ToList();
        Assert.Equal(2, groupTitles.Count);
        Assert.Equal("Apps", groupTitles[1]);
    }

    [Fact]
    public void AppLinks_RenderADefaultSwitcherWithTheCurrentApp()
    {
        var cut = RenderLayout(parameters => parameters
            .Add(p => p.AppLinks, Links())
            .Add(p => p.CurrentAppKey, "dev"));

        Assert.Equal("dev", cut.Find(".base-app-switcher-chip-label").TextContent);
        Assert.Equal("dev.svg", cut.Find("img.base-app-switcher-chip-icon").GetAttribute("src"));
        Assert.Empty(cut.FindAll(".base-header-title"));
        Assert.Contains("brand-start", cut.Find("header").ClassList);
    }

    [Fact]
    public void ACustomSwitcherWithoutGroups_UsesTheLayoutLinks()
    {
        var cut = RenderLayout(parameters => parameters
            .Add(p => p.AppLinks, Links())
            .Add(p => p.CurrentAppKey, "my")
            .Add(p => p.AppSwitcher, builder =>
            {
                builder.OpenComponent<BaseAppSwitcher>(0);
                builder.AddAttribute(1, nameof(BaseAppSwitcher.AriaLabel), "Custom label");
                builder.CloseComponent();
            }));

        var chip = cut.Find("button.base-app-switcher-chip");
        Assert.Equal("my", cut.Find(".base-app-switcher-chip-label").TextContent);
        Assert.Equal("Custom label", chip.GetAttribute("aria-label"));
    }

    [Fact]
    public void ASwitcherWithItsOwnGroups_IgnoresTheLayoutLinks()
    {
        IReadOnlyList<AppLinkGroup> own =
        [
            new() { Key = "own", Label = "Own", Links = [new() { Key = "x", Label = "Own app", Href = "https://x.example/" }] },
        ];

        var cut = RenderLayout(parameters => parameters
            .Add(p => p.AppLinks, Links())
            .Add(p => p.CurrentAppKey, "x")
            .Add(p => p.AppSwitcher, builder =>
            {
                builder.OpenComponent<BaseAppSwitcher>(0);
                builder.AddAttribute(1, nameof(BaseAppSwitcher.Groups), own);
                builder.CloseComponent();
            }));

        Assert.Equal("Own app", cut.Find(".base-app-switcher-chip-label").TextContent);
    }

    [Fact]
    public void AnExplicitSwitcherFragment_StillWinsOverTheDefaultOne()
    {
        var cut = RenderLayout(parameters => parameters
            .Add(p => p.AppLinks, Links())
            .Add(p => p.AppSwitcher, "<span id=\"mine\">mine</span>"));

        Assert.Equal("mine", cut.Find("#mine").TextContent);
        Assert.Empty(cut.FindAll(".base-app-switcher"));
    }

    [Fact]
    public void WithoutAppLinksAndFragment_NoSwitcherIsShown()
    {
        var cut = RenderLayout();

        Assert.Empty(cut.FindAll(".base-app-switcher"));
        Assert.Contains("brand-centered", cut.Find("header").ClassList);
    }

    [Fact]
    public void ShowCommandPaletteWithASwitcher_KeepsTheSwitcherAndAddsTheSearch()
    {
        var cut = RenderLayout(parameters => parameters
            .Add(p => p.ShowCommandPalette, true)
            .Add(p => p.AppLinks, Links())
            .Add(p => p.CurrentAppKey, "dev"));

        Assert.NotNull(cut.Find(".base-header-start .base-app-switcher"));
        Assert.NotNull(cut.Find(".base-header-center.base-header-search"));
    }

    [Fact]
    public void ThePaletteComponent_IsRenderedOnlyWhileEnabled()
    {
        var disabled = RenderLayout();
        Assert.Empty(disabled.FindComponents<BaseCommandPalette>());

        var enabled = RenderLayout(parameters => parameters.Add(p => p.ShowCommandPalette, true));
        Assert.Single(enabled.FindComponents<BaseCommandPalette>());
    }
}
