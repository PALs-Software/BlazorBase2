using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.SavedFilters;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The <c>?preset=</c> deep link of <see cref="BaseList{TModel}"/>.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class PresetDeepLinkTests : BunitTestContextBase
{
    private static readonly string ListKey = typeof(TestProduct).FullName! + "@/products";

    private static FilterGroupModel NameEquals(string value) => new()
    {
        Conditions = [new FilterConditionModel { PropertyName = nameof(TestProduct.Name), Operator = FilterOperator.Equals, Value = value }]
    };

    private static ListFilterDefaults CodeDefaults() => new ListFilterDefaultsBuilder<TestProduct>()
        .Preset("pending", "Only pending", filter => filter.Is(product => product.Name, "Pending"), isDefault: true)
        .Preset("active", "Only active", filter => filter.Is(product => product.Name, "Active"))
        .Build();

    private static string? FirstValueOf(BaseQuery query) => query.Filters.SingleOrDefault()?.Filters?.SingleOrDefault()?.Value?.ToString();

    private static IBaseDataProvider<TestProduct> Provider()
    {
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct> { Items = [new TestProduct { Id = Guid.NewGuid(), Name = "Alpha" }], TotalCount = 1 });
        return provider;
    }

    private IRenderedComponent<BaseList<TestProduct>> RenderList(
        IBaseDataProvider<TestProduct> provider,
        string route,
        ListFilterDefaults? defaults = null,
        bool enableDeepLinking = true)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
        Services.AddLocalization();
        Services.AddSingleton(provider);
        Services.GetRequiredService<NavigationManager>().NavigateTo(route);

        return Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.FilterDefaults, defaults ?? CodeDefaults())
            .Add(p => p.EnableDeepLinking, enableDeepLinking)
            .Add(p => p.ChildContent, BuildColumn()));
    }

    private static RenderFragment BuildColumn() => builder =>
    {
        builder.OpenComponent<BaseColumn<TestProduct>>(0);
        builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), (Expression<Func<TestProduct, object?>>)(product => product.Name));
        builder.CloseComponent();
    };

    [Fact]
    public void KnownPreset_IsAppliedOnFirstLoad_InsteadOfTheCodeDefault()
    {
        var provider = Provider();

        RenderList(provider, "/Products?preset=active");

        Assert.Equal("Active", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void KnownPreset_IsMatchedCaseInsensitively()
    {
        var provider = Provider();

        RenderList(provider, "/Products?preset=ACTIVE");

        Assert.Equal("Active", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void UnknownPreset_IsIgnoredSilently_AndTheCodeDefaultApplies()
    {
        var provider = Provider();

        var cut = RenderList(provider, "/Products?preset=does-not-exist");

        Assert.Equal("Pending", FirstValueOf(provider.ReceivedQueries().First()));
        Assert.Empty(cut.FindAll(".base-list-error"));
    }

    [Fact]
    public void NoPresetParameter_KeepsTheCodeDefault()
    {
        var provider = Provider();

        RenderList(provider, "/Products");

        Assert.Equal("Pending", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void KnownPreset_WinsOverTheUsersSavedDefault()
    {
        var store = new InMemoryListFilterStore();
        store.SaveAsync(ListKey, new SavedListFilterInput { Name = "Mine", Filter = NameEquals("Mine"), SetAsDefault = true }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();

        RenderList(provider, "/Products?preset=active");

        Assert.Equal("Active", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void KnownPreset_IsNotStoredAsTheUsersDefault()
    {
        var store = new InMemoryListFilterStore();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();

        RenderList(provider, "/Products?preset=active");

        var saved = store.GetAsync(ListKey).GetAwaiter().GetResult();
        Assert.Empty(saved);
    }

    [Fact]
    public void UnknownPreset_KeepsTheUsersSavedDefault()
    {
        var store = new InMemoryListFilterStore();
        store.SaveAsync(ListKey, new SavedListFilterInput { Name = "Mine", Filter = NameEquals("Mine"), SetAsDefault = true }).GetAwaiter().GetResult();
        Services.AddSingleton<IListFilterStore>(store);
        var provider = Provider();

        RenderList(provider, "/Products?preset=nope");

        Assert.Equal("Mine", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void KnownPreset_TogetherWithAnItemParameter_StillFiltersTheList()
    {
        var provider = Provider();

        RenderList(provider, $"/Products?item={Guid.NewGuid()}&preset=active");

        Assert.Equal("Active", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void KnownPreset_BeforeTheItemParameter_StillFiltersTheList()
    {
        var provider = Provider();

        RenderList(provider, $"/Products?preset=active&item={Guid.NewGuid()}");

        Assert.Equal("Active", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void KnownPreset_IsShownAsTheActiveChoiceOfTheQuickFilter()
    {
        var provider = Provider();

        var cut = RenderList(provider, "/Products?preset=active");

        cut.WaitForAssertion(() =>
        {
            var selected = cut.FindAll("fluent-option[selected]");
            Assert.Contains(selected, option => option.GetAttribute("value") == "preset:active");
        });
    }

    [Fact]
    public void ListWithoutDeepLinking_IgnoresThePresetParameter()
    {
        var provider = Provider();

        RenderList(provider, "/Products?preset=active", enableDeepLinking: false);

        Assert.Equal("Pending", FirstValueOf(provider.ReceivedQueries().First()));
    }

    [Fact]
    public void ListWithoutPresets_IgnoresThePresetParameter()
    {
        var provider = Provider();

        RenderList(provider, "/Products?preset=active", new ListFilterDefaults());

        Assert.Empty(provider.ReceivedQueries().First().Filters);
    }

    [Fact]
    public void AnEmptyPreset_OverridesTheDefaultWithAnUnfilteredList()
    {
        var defaults = new ListFilterDefaults
        {
            Presets =
            [
                new FilterPreset { Id = "pending", Name = "Pending", Filter = NameEquals("Pending"), IsDefault = true },
                new FilterPreset { Id = "all", Name = "All" },
            ]
        };
        var provider = Provider();

        RenderList(provider, "/Products?preset=all", defaults);

        Assert.Empty(provider.ReceivedQueries().First().Filters);
    }
}
