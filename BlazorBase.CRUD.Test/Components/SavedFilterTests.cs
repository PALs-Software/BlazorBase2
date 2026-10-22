using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.SavedFilters;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// Saved filters, presets and defaults inside <see cref="BaseList{TModel}"/> and its filter drawer.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class SavedFilterTests : BunitTestContextBase
{
    private static readonly string ListKey = typeof(TestProduct).FullName! + "@/";

    private static FilterGroupModel NameEquals(string value) => new()
    {
        Conditions = [new FilterConditionModel { PropertyName = nameof(TestProduct.Name), Operator = FilterOperator.Equals, Value = value }]
    };

    private static ListFilterDefaults CodeDefaults() => new ListFilterDefaultsBuilder<TestProduct>()
        .Preset("pending", "Only pending", filter => filter.Is(product => product.Name, "Pending"), isDefault: true)
        .Preset("active", "Only active", filter => filter.Is(product => product.Name, "Active"))
        .Build();

    private static string? FirstValueOf(BaseQuery query) => query.Filters.SingleOrDefault()?.Filters?.SingleOrDefault()?.Value?.ToString();

    private IBaseDataProvider<TestProduct> Provider()
    {
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct> { Items = [new TestProduct { Id = Guid.NewGuid(), Name = "Alpha" }], TotalCount = 1 });
        return provider;
    }

    private IRenderedComponent<BaseList<TestProduct>> RenderList(
        IBaseDataProvider<TestProduct> provider,
        ListFilterDefaults? defaults = null,
        bool enableSavedFilters = true,
        string? route = null)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
        Services.AddLocalization();
        Services.AddSingleton(provider);

        if (route is not null)
            Services.GetRequiredService<NavigationManager>().NavigateTo(route);

        return Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.FilterDefaults, defaults)
            .Add(p => p.EnableSavedFilters, enableSavedFilters)
            .Add(p => p.ChildContent, BuildColumn()));
    }

    private static RenderFragment BuildColumn() => builder =>
    {
        builder.OpenComponent<BaseColumn<TestProduct>>(0);
        builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), (Expression<Func<TestProduct, object?>>)(product => product.Name));
        builder.CloseComponent();
    };

    private static void OpenDrawer(IRenderedComponent<BaseList<TestProduct>> cut)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-filter-toggle")));
        cut.Find(".base-list-filter-toggle").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".filter-panel")));
    }

    private static void ExpandSavedFilters(IRenderedComponent<BaseList<TestProduct>> cut)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".saved-filters-toggle")));
        cut.Find(".saved-filters-toggle").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".saved-filters-body")));
    }

    [Fact]
    public void OpeningAList_ReadsTheSavedFiltersOnce()
    {
        var store = Substitute.For<IListFilterStore>();
        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<SavedListFilterInfo>>([]));
        Services.AddSingleton(store);
        var provider = Provider();

        var cut = RenderList(provider);

        cut.WaitForAssertion(() => Assert.NotEmpty(provider.ReceivedQueries()));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-filter-toggle")));
        store.Received(1).GetAsync(ListKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadSavedAsync_SharesOneReadPerRevision_AndReadsAgainForANewOne()
    {
        var store = Substitute.For<IListFilterStore>();
        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<SavedListFilterInfo>>([]));
        var context = new SavedFilterContext(ListKey, store, null, [], Substitute.For<Microsoft.Extensions.Localization.IStringLocalizer>());

        await context.ReadSavedAsync(0);
        await context.ReadSavedAsync(0);
        await store.Received(1).GetAsync(ListKey, Arg.Any<CancellationToken>());

        await context.ReadSavedAsync(1);
        await store.Received(2).GetAsync(ListKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void NothingConfigured_ShowsNoSavedFilterSection()
    {
        var cut = RenderList(Provider());

        OpenDrawer(cut);

        Assert.Empty(cut.FindAll(".saved-filters"));
    }

    [Fact]
    public void CodeDefault_IsAppliedOnFirstLoad_AndCountsAsActiveFilter()
    {
        var provider = Provider();

        var cut = RenderList(provider, CodeDefaults());

        cut.WaitForAssertion(() => Assert.Equal("Pending", FirstValueOf(provider.ReceivedQueries().First())));
        Assert.Equal("1", cut.Find(".base-list-filter-count").TextContent.Trim());
    }

    [Fact]
    public void UserDefault_WinsOverTheCodeDefault()
    {
        var store = new InMemoryListFilterStore();
        store.SaveAsync(ListKey, new SavedListFilterInput { Name = "Mine", Filter = NameEquals("Mine"), SetAsDefault = true }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();

        RenderList(provider, CodeDefaults());

        Assert.Equal("Mine", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void ADefaultSavedOnAnotherPageOverTheSameModel_DoesNotApplyHere()
    {
        var store = new InMemoryListFilterStore();
        store.SaveAsync(typeof(TestProduct).FullName + "@/enrolledproducts", new SavedListFilterInput { Name = "Mine", Filter = NameEquals("Mine"), SetAsDefault = true }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();

        RenderList(provider, route: "/Products?page=3");

        Assert.Empty(provider.ReceivedQueries().First().Filters);
    }

    [Fact]
    public void TheDefaultSavedForThisPagesRoute_IsApplied()
    {
        var store = new InMemoryListFilterStore();
        store.SaveAsync(typeof(TestProduct).FullName + "@/products", new SavedListFilterInput { Name = "Route", Filter = NameEquals("Route"), SetAsDefault = true }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();

        RenderList(provider, route: "/Products/?x=1");

        Assert.Equal("Route", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void AnExplicitKey_WinsOverTheRoute()
    {
        var store = new InMemoryListFilterStore();
        store.SaveAsync(typeof(TestProduct).FullName + "@/products", new SavedListFilterInput { Name = "Route", Filter = NameEquals("Route"), SetAsDefault = true }).GetAwaiter().GetResult();
        store.SaveAsync("shared-products", new SavedListFilterInput { Name = "Explicit", Filter = NameEquals("Explicit"), SetAsDefault = true }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();

        RenderList(provider, new ListFilterDefaultsBuilder<TestProduct>().ListKey("shared-products").Build(), route: "/Products");

        Assert.Equal("Explicit", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void UserDefaultWithAnEmptyFilter_SuppressesTheCodeDefault()
    {
        var store = new InMemoryListFilterStore();
        store.SaveAsync(ListKey, new SavedListFilterInput { Name = "All", Filter = new FilterGroupModel(), SetAsDefault = true }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();

        RenderList(provider, CodeDefaults());

        Assert.Empty(provider.ReceivedQueries().First().Filters);
    }

    [Fact]
    public void ADefaultNamingAFieldTheListNoLongerOffers_IsIgnoredInsteadOfBreakingTheQuery()
    {
        var store = new InMemoryListFilterStore();
        var stale = new FilterGroupModel
        {
            Conditions = [new FilterConditionModel { PropertyName = "Removed", Operator = FilterOperator.Equals, Value = "x" }]
        };
        store.SaveAsync(ListKey, new SavedListFilterInput { Name = "Old", Filter = stale, SetAsDefault = true }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();

        RenderList(provider);

        Assert.Empty(provider.ReceivedQueries().First().Filters);
    }

    [Fact]
    public void UnreachableStore_FallsBackToTheCodeDefault()
    {
        var store = Substitute.For<IListFilterStore>();
        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<SavedListFilterInfo>>>(_ => throw new HttpRequestException("down"));
        Services.AddSingleton(store);
        var provider = Provider();

        RenderList(provider, CodeDefaults());

        Assert.Equal("Pending", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void Drawer_ListsPresets_AndLoadingOneAppliesItAndClosesTheDrawer()
    {
        var provider = Provider();
        var cut = RenderList(provider, CodeDefaults());
        OpenDrawer(cut);
        ExpandSavedFilters(cut);

        var presets = cut.FindAll(".saved-filters-load");
        Assert.Equal(2, presets.Count);
        Assert.Contains("Only pending", presets[0].TextContent);
        Assert.NotEmpty(presets[0].QuerySelectorAll(".saved-filters-tag"));

        presets[1].Click();

        cut.WaitForAssertion(() => Assert.Equal("Active", FirstValueOf(provider.ReceivedQueries().Last())));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".filter-panel")));
    }

    [Fact]
    public void Drawer_SavesTheCurrentFilterUnderAName_ThenListsAndLoadsIt()
    {
        var store = new InMemoryListFilterStore();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();
        var cut = RenderList(provider, new ListFilterDefaultsBuilder<TestProduct>().DefaultFilter(filter => filter.Is(product => product.Name, "Seed")).Build());
        OpenDrawer(cut);
        ExpandSavedFilters(cut);

        cut.Find(".saved-filters-save fluent-text-field").Input("My view");
        cut.FindAll(".saved-filters-save fluent-button").Last(button => button.TextContent.Contains("Save") || button.TextContent.Contains("Speichern") || button.TextContent.Contains("SavedFiltersSave")).Click();

        cut.WaitForAssertion(() => Assert.Single(store.GetAsync(ListKey).GetAwaiter().GetResult()));
        var saved = store.GetAsync(ListKey).GetAwaiter().GetResult().Single();
        Assert.Equal("My view", saved.Name);
        Assert.Equal("Seed", saved.Filter.Conditions.Single().Value);
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll(".saved-filters-name"), name => name.TextContent == "My view"));
    }

    [Fact]
    public void Drawer_DefaultStarDeleteAndRename_UpdateTheStore()
    {
        var store = new InMemoryListFilterStore();
        store.SaveAsync(ListKey, new SavedListFilterInput { Name = "Mine", Filter = NameEquals("m") }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var cut = RenderList(Provider());
        OpenDrawer(cut);
        ExpandSavedFilters(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".saved-filters-icon")));

        cut.FindAll(".saved-filters-icon")[0].Click();
        cut.WaitForAssertion(() => Assert.True(store.GetAsync(ListKey).GetAwaiter().GetResult().Single().IsDefault));

        cut.FindAll(".saved-filters-icon")[1].Click();
        cut.Find(".saved-filters-edit fluent-text-field").Input("Renamed");
        cut.FindAll(".saved-filters-edit fluent-button").First().Click();
        cut.WaitForAssertion(() => Assert.Equal("Renamed", store.GetAsync(ListKey).GetAwaiter().GetResult().Single().Name));

        cut.FindAll(".saved-filters-icon")[2].Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".saved-filters-confirm")));
        cut.FindAll(".saved-filters-edit fluent-button").First().Click();
        cut.WaitForAssertion(() => Assert.Empty(store.GetAsync(ListKey).GetAwaiter().GetResult()));
    }

    [Fact]
    public void DisablingSavedFilters_KeepsPresetsButIgnoresTheStore()
    {
        var store = new InMemoryListFilterStore();
        store.SaveAsync(ListKey, new SavedListFilterInput { Name = "Mine", Filter = NameEquals("Mine"), SetAsDefault = true }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();

        var cut = RenderList(provider, CodeDefaults(), enableSavedFilters: false);

        Assert.Equal("Pending", FirstValueOf(provider.ReceivedQueries().First()));
        OpenDrawer(cut);
        ExpandSavedFilters(cut);
        Assert.Empty(cut.FindAll(".saved-filters-save"));
    }

    [Fact]
    public void QuickFilter_IsHiddenWhileThereIsNothingToChooseFrom()
    {
        var cut = RenderList(Provider());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-filter-toggle")));
        Assert.Empty(cut.FindAll(".quick-filter"));
    }

    private static IRenderedComponent<FluentSelect<string>> QuickSelect(IRenderedComponent<BaseList<TestProduct>> cut) =>
        cut.FindComponents<FluentSelect<string>>().Single(select => select.Instance.Class == "quick-filter");

    private static void Choose(IRenderedComponent<BaseList<TestProduct>> cut, string key) =>
        cut.InvokeAsync(() => QuickSelect(cut).Instance.ValueChanged.InvokeAsync(key)).GetAwaiter().GetResult();

    [Fact]
    public void QuickFilter_ShowsTheActivePreset_AndSwitchingAppliesTheOtherOneWithoutTheDrawer()
    {
        var provider = Provider();
        var cut = RenderList(provider, CodeDefaults());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".quick-filter")));
        Assert.Equal("preset:pending", QuickSelect(cut).Instance.Value);
        Assert.Equal(3, cut.FindAll(".quick-filter fluent-option").Count);

        Choose(cut, "preset:active");

        cut.WaitForAssertion(() => Assert.Equal("Active", FirstValueOf(provider.ReceivedQueries().Last())));
        cut.WaitForAssertion(() => Assert.Equal("preset:active", QuickSelect(cut).Instance.Value));
        Assert.Empty(cut.FindAll(".filter-panel"));
    }

    [Fact]
    public void QuickFilter_NoFilterEntry_ClearsTheFilter()
    {
        var provider = Provider();
        var cut = RenderList(provider, CodeDefaults());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".quick-filter")));

        Choose(cut, "none");

        cut.WaitForAssertion(() => Assert.Null(FirstValueOf(provider.ReceivedQueries().Last())));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-list-filter-count")));
        Assert.Equal("none", QuickSelect(cut).Instance.Value);
    }

    [Fact]
    public void QuickFilter_ListsTheUsersSavedFilters()
    {
        var store = new InMemoryListFilterStore();
        store.SaveAsync(ListKey, new SavedListFilterInput { Name = "Mine", Filter = NameEquals("Mine") }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();
        var cut = RenderList(provider);
        var saved = store.GetAsync(ListKey).GetAwaiter().GetResult().Single();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".quick-filter")));
        Choose(cut, "saved:" + saved.Id);

        cut.WaitForAssertion(() => Assert.Equal("Mine", FirstValueOf(provider.ReceivedQueries().Last())));
        cut.WaitForAssertion(() => Assert.Equal("saved:" + saved.Id, QuickSelect(cut).Instance.Value));
    }

    [Fact]
    public void QuickFilter_OffersACustomEntryWhileTheFilterMatchesNoChoice()
    {
        var provider = Provider();
        var cut = RenderList(provider, new ListFilterDefaultsBuilder<TestProduct>()
            .Preset("active", "Only active", filter => filter.Is(product => product.Name, "Active"))
            .DefaultFilter(filter => filter.Is(product => product.Name, "Other"))
            .Build());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".quick-filter")));

        Assert.Equal("custom", QuickSelect(cut).Instance.Value);
    }
}
