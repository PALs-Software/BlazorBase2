using System.Security.Claims;
using BlazorBase.Components.CommandPalette;
using BlazorBase.Components.Layout.AppSwitcher;
using BlazorBase.Components.Layout.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace BlazorBase.Components.Test.CommandPalette;

internal sealed class KeyLocalizer : IStringLocalizer<BaseCommandPalette>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}

public sealed class CommandPaletteProviderTests
{
    private static ClaimsPrincipal UserWithRoles(params string[] roles) =>
        new(new ClaimsIdentity(roles.Select(role => new Claim(ClaimTypes.Role, role)), "test"));

    private static CommandPaletteRequest Request(ClaimsPrincipal user) =>
        new() { Query = string.Empty, User = user, Culture = System.Globalization.CultureInfo.InvariantCulture };

    private static IReadOnlyList<NavigationItem> SampleNavigation() =>
    [
        new() { Href = "/", Label = "Home", MatchAll = true },
        new()
        {
            Label = "GitHub Copilot",
            Children =
            [
                new() { Href = "/budgets", Label = "Budgets", ShortLabel = "Budget" },
                new() { Href = "/seats", Label = "Seats", RequiredRole = "Admin" },
            ],
        },
        new()
        {
            Label = "Administration",
            RequiredRole = "Admin",
            Children =
            [
                new() { Label = "Security", Children = [new() { Href = "/admin/roles", Label = "Roles" }] },
                new() { Href = "/admin/users", Label = "Users", RequiredRole = "Auditor" },
            ],
        },
        new() { Label = "Empty group" },
        new() { Label = "Sign out", IsDanger = true, OnClick = () => Task.CompletedTask },
    ];

    private static async Task<IReadOnlyList<CommandPaletteEntry>> NavigationEntriesAsync(ClaimsPrincipal user) =>
        await new NavigationCommandProvider(SampleNavigation(), new KeyLocalizer()).GetEntriesAsync(Request(user), CancellationToken.None);

    [Fact]
    public async Task Navigation_FlattensTheTreeWithBreadcrumbsAndSkipsGroups()
    {
        var entries = await NavigationEntriesAsync(UserWithRoles("Admin", "Auditor"));

        Assert.Equal(
            ["Home", "Budgets", "Seats", "Roles", "Users", "Sign out"],
            entries.Select(entry => entry.Title));
        Assert.Null(entries[0].Subtitle);
        Assert.Equal("GitHub Copilot", entries[1].Subtitle);
        Assert.Equal("Administration › Security", entries[3].Subtitle);
        Assert.All(entries.Take(5), entry => Assert.StartsWith("page:", entry.Id));
        Assert.Equal("GroupPages", entries[1].Group);
        Assert.Equal("HintPage", entries[1].Hint);
        Assert.Equal("/budgets", entries[1].Href);
    }

    [Fact]
    public async Task Navigation_NumbersTheEntriesInTheOrderOfTheTree()
    {
        var entries = await NavigationEntriesAsync(UserWithRoles("Admin", "Auditor"));

        Assert.Equal([0, 1, 2, 3, 4, 5], entries.Select(entry => entry.Order));
    }

    [Fact]
    public async Task Navigation_Order_IgnoresEntriesTheUserMayNotSee()
    {
        var entries = await NavigationEntriesAsync(UserWithRoles("Auditor"));

        Assert.Equal(["Home", "Budgets", "Sign out"], entries.Select(entry => entry.Title));
        Assert.Equal([0, 1, 2], entries.Select(entry => entry.Order));
    }

    [Fact]
    public async Task Navigation_TurnsActionsIntoExecuteEntriesInTheirOwnGroup()
    {
        var entries = await NavigationEntriesAsync(UserWithRoles("Admin", "Auditor"));

        var signOut = entries[^1];
        Assert.Equal("action:Sign out", signOut.Id);
        Assert.Equal("GroupActions", signOut.Group);
        Assert.Equal("HintAction", signOut.Hint);
        Assert.NotNull(signOut.Execute);
        Assert.Null(signOut.Href);
        Assert.True(signOut.GroupOrder > entries[0].GroupOrder);
    }

    [Fact]
    public async Task Navigation_FiltersByTheRoleOfTheEntryAndOfEveryParent()
    {
        var anonymous = await NavigationEntriesAsync(new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.Equal(["Home", "Budgets", "Sign out"], anonymous.Select(entry => entry.Title));

        var admin = await NavigationEntriesAsync(UserWithRoles("Admin"));
        Assert.Equal(["Home", "Budgets", "Seats", "Roles", "Sign out"], admin.Select(entry => entry.Title));

        var auditor = await NavigationEntriesAsync(UserWithRoles("Auditor"));
        Assert.Equal(["Home", "Budgets", "Sign out"], auditor.Select(entry => entry.Title));
    }

    [Fact]
    public async Task Navigation_KeepsTheRoleOnTheEntryAsASecondGate()
    {
        var entries = await NavigationEntriesAsync(UserWithRoles("Admin"));

        Assert.Equal("Admin", entries.Single(entry => entry.Title == "Seats").RequiredRole);
    }

    [Fact]
    public async Task Navigation_OffersTheShortLabelAsAKeyword()
    {
        var entries = await NavigationEntriesAsync(UserWithRoles("Admin"));

        Assert.Equal(["Budget"], entries.Single(entry => entry.Title == "Budgets").Keywords);
        Assert.Null(entries[0].Keywords);
    }

    [Fact]
    public async Task AppLinks_AreOfferedUnderAppsWithTheGroupAsSubtitle()
    {
        IReadOnlyList<AppLinkGroup> groups =
        [
            new()
            {
                Key = "cosmos",
                Label = "My Cosmos",
                Links =
                [
                    new() { Key = "my", Label = "my", Href = "https://my.example/", IconSrc = "my.svg", Description = "Home of the cosmos" },
                    new() { Key = "docs", Label = "Docs", Href = "https://docs.example/", OpenInNewTab = false },
                ],
            },
        ];

        var entries = await new AppLinkCommandProvider(groups, new KeyLocalizer()).GetEntriesAsync(Request(UserWithRoles()), CancellationToken.None);

        Assert.Equal(["my", "Docs"], entries.Select(entry => entry.Title));
        var first = entries[0];
        Assert.Equal("app:cosmos/my", first.Id);
        Assert.Equal("My Cosmos", first.Subtitle);
        Assert.Equal("GroupApps", first.Group);
        Assert.Equal("HintApp", first.Hint);
        Assert.Equal("https://my.example/", first.Href);
        Assert.Equal("my.svg", first.IconSrc);
        Assert.Equal(["Home of the cosmos"], first.Keywords);
        Assert.True(first.OpenInNewTab);
        Assert.False(entries[1].OpenInNewTab);
    }

    [Fact]
    public async Task AppLinks_AreNumberedAcrossGroupsInTheirOrder()
    {
        IReadOnlyList<AppLinkGroup> groups =
        [
            new() { Key = "one", Label = "One", Links = [new() { Key = "a", Label = "A", Href = "https://a.example/" }, new() { Key = "b", Label = "B", Href = "https://b.example/" }] },
            new() { Key = "two", Label = "Two", Links = [new() { Key = "c", Label = "C", Href = "https://c.example/" }] },
        ];

        var entries = await new AppLinkCommandProvider(groups, new KeyLocalizer()).GetEntriesAsync(Request(UserWithRoles()), CancellationToken.None);

        Assert.Equal([0, 1, 2], entries.Select(entry => entry.Order));
    }

    [Fact]
    public async Task AppLinks_HonorTheRolesOfGroupsAndLinks()
    {
        IReadOnlyList<AppLinkGroup> groups =
        [
            new() { Key = "public", Label = "Public", Links = [new() { Key = "a", Label = "A", Href = "https://a.example/" }] },
            new()
            {
                Key = "internal",
                Label = "Internal",
                RequiredRole = "Employee",
                Links =
                [
                    new() { Key = "b", Label = "B", Href = "https://b.example/" },
                    new() { Key = "c", Label = "C", Href = "https://c.example/", RequiredRole = "Admin" },
                ],
            },
        ];
        var provider = new AppLinkCommandProvider(groups, new KeyLocalizer());

        var employee = await provider.GetEntriesAsync(Request(UserWithRoles("Employee")), CancellationToken.None);
        var admin = await provider.GetEntriesAsync(Request(UserWithRoles("Employee", "Admin")), CancellationToken.None);
        var stranger = await provider.GetEntriesAsync(Request(UserWithRoles()), CancellationToken.None);

        Assert.Equal(["A", "B"], employee.Select(entry => entry.Title));
        Assert.Equal(["A", "B", "C"], admin.Select(entry => entry.Title));
        Assert.Equal(["A"], stranger.Select(entry => entry.Title));
    }

    [Fact]
    public async Task Registration_OffersProviderClassesFunctionsAndStaticLists()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCommandPaletteProvider<ClassProvider>();
        services.AddCommandPaletteProvider((request, _) => ValueTask.FromResult<IReadOnlyList<CommandPaletteEntry>>(
            [new CommandPaletteEntry { Id = "fn", Title = "Function " + request.Query.Length, Group = "G", Href = "/fn" }]));
        services.AddCommandPaletteProvider([new CommandPaletteEntry { Id = "static", Title = "Static", Group = "G", Href = "/static" }]);
        services.AddBlazorBaseCommandPalette(options => options.MaxResults = 12);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var providers = scope.ServiceProvider.GetServices<ICommandPaletteProvider>().ToList();
        var entries = (await Task.WhenAll(providers.Select(p => p.GetEntriesAsync(Request(UserWithRoles()), CancellationToken.None).AsTask())))
            .SelectMany(list => list)
            .Select(entry => entry.Id)
            .Order()
            .ToList();

        Assert.Equal(3, providers.Count);
        Assert.Equal(["class", "fn", "static"], entries);
        Assert.Equal(12, scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CommandPaletteOptions>>().Value.MaxResults);
        Assert.NotSame(
            scope.ServiceProvider.GetRequiredService<ICommandPaletteService>(),
            provider.CreateScope().ServiceProvider.GetRequiredService<ICommandPaletteService>());
    }

    private sealed class ClassProvider : ICommandPaletteProvider
    {
        public ValueTask<IReadOnlyList<CommandPaletteEntry>> GetEntriesAsync(CommandPaletteRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<CommandPaletteEntry>>([new CommandPaletteEntry { Id = "class", Title = "Class", Group = "G", Href = "/class" }]);
    }
}

public sealed class CommandPaletteServiceTests
{
    [Fact]
    public async Task OpenAndClose_ToggleIsOpenAndRaiseChanged()
    {
        var service = new CommandPaletteService();
        var changes = 0;
        service.Changed += () => changes++;

        await service.OpenAsync("bud");
        Assert.True(service.IsOpen);
        Assert.Equal("bud", service.InitialQuery);

        await service.CloseAsync();
        Assert.False(service.IsOpen);
        Assert.Null(service.InitialQuery);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task OpeningAnOpenPaletteWithoutAQuery_RaisesNothing()
    {
        var service = new CommandPaletteService();
        await service.OpenAsync();
        var changes = 0;
        service.Changed += () => changes++;

        await service.OpenAsync();
        await service.OpenAsync("new text");

        Assert.Equal(1, changes);
        Assert.Equal("new text", service.InitialQuery);
    }

    [Fact]
    public async Task ClosingAClosedPalette_RaisesNothing()
    {
        var service = new CommandPaletteService();
        var changes = 0;
        service.Changed += () => changes++;

        await service.CloseAsync();

        Assert.Equal(0, changes);
    }
}
