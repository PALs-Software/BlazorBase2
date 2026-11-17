using System.Linq.Expressions;
using BlazorBase.Components.Test.Infrastructure;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.SavedFilters;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The quick search of a list: opt-in by naming the properties to search (or asking for the display keys), one OR group of
/// "contains" conditions combined by AND with the list's other filters, the list starting again from its first row, and no
/// box at all for a list without anything to search.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListQuickSearchTests : BunitTestContextBase
{
    private const string BoxSelector = ".base-list-quick-search";

    private readonly List<TestProduct> catalog =
    [
        new() { Id = Guid.NewGuid(), Name = "Alpha Widget", Description = "green", IsActive = true },
        new() { Id = Guid.NewGuid(), Name = "Beta Gadget", Description = "Widget accessory", IsActive = false },
        new() { Id = Guid.NewGuid(), Name = "Gamma", Description = "blue", IsActive = true }
    ];

    private IStringLocalizer Framework => Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));

    [Fact]
    public void AListWithoutNamedProperties_ShowsNoSearchBox()
    {
        var cut = RenderList(ProviderFor([]));

        Assert.Empty(cut.FindAll(BoxSelector));
    }

    [Fact]
    public void NamingTheProperties_ShowsTheSearchBox_WithTheLocalizedPlaceholder()
    {
        var cut = RenderList(ProviderFor(catalog), quickSearchProperties: ["Name"]);

        var input = cut.FindComponent<FluentTextInput>();

        Assert.Single(cut.FindAll(BoxSelector));
        Assert.Equal(Framework["ListSearchPlaceholder"].Value, input.Instance.Placeholder);
    }

    [Fact]
    public void TheSearchBox_SitsInTheToolbar_BeforeTheFilterGroup()
    {
        var cut = RenderList(ProviderFor(catalog), quickSearchProperties: ["Name"]);

        var children = cut.Find(".base-list-header").Children.ToList();

        Assert.Contains(children, child => child.ClassList.Contains("base-list-quick-search"));
        Assert.Empty(cut.FindAll(".base-list-search-row"));
    }

    [Fact]
    public void ShowFalse_HidesTheBox_EvenWhenPropertiesAreNamed()
    {
        var cut = RenderList(ProviderFor(catalog), quickSearchProperties: ["Name"], showQuickSearch: false);

        Assert.Empty(cut.FindAll(BoxSelector));
    }

    [Fact]
    public void ShowTrue_WithoutNamedProperties_SearchesTheDisplayKeyProperties()
    {
        var provider = PrintingProvider();
        var cut = RenderPrintingList(provider, showQuickSearch: true);

        Type(cut, "x1");

        cut.WaitForAssertion(() =>
        {
            var filter = Assert.Single(provider.ReceivedQueries<TestPrinting>().Last().Filters);
            Assert.Equal(FilterLogic.Or, filter.Logic);
            Assert.Equal(["Code", "SetName"], filter.Filters!.Select(condition => condition.PropertyName));
        });
    }

    [Fact]
    public void ShowTrue_ForAModelWithoutDisplayKeys_ShowsNoBox()
    {
        var cut = RenderList(ProviderFor(catalog), showQuickSearch: true);

        Assert.Empty(cut.FindAll(BoxSelector));
    }

    [Fact]
    public void ShowNull_WithoutNamedProperties_ShowsNoBox_EvenForAModelWithDisplayKeys()
    {
        var provider = PrintingProvider();
        var cut = RenderPrintingList(provider, showQuickSearch: null);

        Assert.Empty(cut.FindAll(BoxSelector));
    }

    [Fact]
    public void ThePropertiesOfTheListConfiguration_ShowTheBox()
    {
        var configuration = new BaseListBuilder<TestProduct>()
            .Column(product => product.Name)
            .QuickSearch(product => product.Name, product => product.Category!.Name)
            .Build();

        var cut = RenderList(ProviderFor(catalog), configuration: configuration);

        Assert.Equal(["Name", "Category.Name"], configuration.QuickSearchProperties);
        Assert.Single(cut.FindAll(BoxSelector));
    }

    [Fact]
    public void AParameterBeatsTheListConfiguration()
    {
        var configuration = new BaseListBuilder<TestProduct>().Column(product => product.Name).QuickSearch(product => product.Name).Build();
        var provider = ProviderFor(catalog);
        var cut = RenderList(provider, quickSearchProperties: ["Description"], configuration: configuration);

        Type(cut, "wid");

        cut.WaitForAssertion(() => Assert.Equal("Description", Assert.Single(provider.ReceivedQueries<TestProduct>().Last().Filters).PropertyName));
    }

    [Fact]
    public void Typing_FiltersTheList_ByAnyOfTheNamedProperties()
    {
        var provider = ProviderFor(catalog);
        var cut = RenderList(provider, quickSearchProperties: ["Name", "Description"]);
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("tr.base-list-row:not([row-type])").Count));

        Type(cut, "widget");

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tr.base-list-row:not([row-type])").Count));
        var filter = Assert.Single(provider.ReceivedQueries<TestProduct>().Last().Filters);
        Assert.Equal(FilterLogic.Or, filter.Logic);
        Assert.Equal(["Name", "Description"], filter.Filters!.Select(condition => condition.PropertyName));
        Assert.All(filter.Filters!, condition => Assert.Equal("widget", condition.Value));
    }

    [Fact]
    public void ASingleNamedProperty_IsSentAsOnePlainCondition()
    {
        var provider = ProviderFor(catalog);
        var cut = RenderList(provider, quickSearchProperties: ["Name"]);

        Type(cut, "gam");

        cut.WaitForAssertion(() =>
        {
            var filter = Assert.Single(provider.ReceivedQueries<TestProduct>().Last().Filters);
            Assert.Equal("Name", filter.PropertyName);
            Assert.Equal(FilterOperator.Contains, filter.Operator);
            Assert.Equal("gam", filter.Value);
        });
    }

    [Fact]
    public void TheSearch_IsCombinedByAnd_WithTheAdditionalFilters()
    {
        var provider = ProviderFor(catalog);
        var additional = new List<FilterDescriptor> { new() { PropertyName = nameof(TestProduct.IsActive), Operator = FilterOperator.Equals, Value = true } };
        var cut = RenderList(provider, quickSearchProperties: ["Name", "Description"], additionalFilters: additional);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tr.base-list-row:not([row-type])").Count));

        Type(cut, "widget");

        cut.WaitForAssertion(() =>
        {
            var query = provider.ReceivedQueries<TestProduct>().Last();
            Assert.Equal(2, query.Filters.Count);
            Assert.Same(additional[0], query.Filters[0]);
            Assert.True(query.Filters[1].IsGroup);
            Assert.Single(cut.FindAll("tr.base-list-row:not([row-type])"));
        });
    }

    [Fact]
    public void TheSearch_IsCombinedByAnd_WithTheFilterTheListStartsWith()
    {
        var provider = ProviderFor(catalog);
        Services.AddSingleton<IListFilterStore>(new InMemoryListFilterStore());
        var defaults = new ListFilterDefaultsBuilder<TestProduct>().DefaultFilter(filter => filter.Is(product => product.IsActive, true)).Build();
        var cut = RenderList(provider, quickSearchProperties: ["Name", "Description"], filterDefaults: defaults, secondColumn: nameof(TestProduct.IsActive));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tr.base-list-row:not([row-type])").Count));

        Type(cut, "widget");

        cut.WaitForAssertion(() =>
        {
            var query = provider.ReceivedQueries<TestProduct>().Last();
            Assert.Equal(2, query.Filters.Count);
            Assert.Equal(nameof(TestProduct.IsActive), Assert.Single(query.Filters[0].Filters!).PropertyName);
            Assert.Equal(FilterLogic.Or, query.Filters[1].Logic);
            Assert.Single(cut.FindAll("tr.base-list-row:not([row-type])"));
        });
    }

    [Fact]
    public void ARunningSearch_KeepsFilteringTheRowsThatAreLoadedLater()
    {
        var provider = ProviderFor(catalog);
        var cut = RenderList(provider, quickSearchProperties: ["Name"], pageSize: 10);
        Type(cut, "gam");
        cut.WaitForAssertion(() => Assert.Equal("gam", Assert.Single(provider.ReceivedQueries<TestProduct>().Last().Filters).Value));
        var before = provider.ReceivedQueries<TestProduct>().Count();

        cut.InvokeAsync(() => cut.Instance.RefreshAsync()).GetAwaiter().GetResult();

        Assert.All(provider.ReceivedQueries<TestProduct>().Skip(before), query => Assert.Equal("gam", Assert.Single(query.Filters).Value));
    }

    [Fact]
    public void Searching_LoadsTheListAgainFromItsFirstRow()
    {
        var provider = PagedProvider(total: 1000);
        var cut = RenderList(provider, quickSearchProperties: ["Name"], pageSize: 50);
        WaitForQueriesToSettle(provider);
        cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(800, 20, CancellationToken.None, debounce: false)).GetAwaiter().GetResult();
        WaitForQueriesToSettle(provider);
        var before = provider.ReceivedQueries<TestProduct>().Count();

        Type(cut, "abc");

        cut.WaitForAssertion(() =>
        {
            var firstSearchQuery = provider.ReceivedQueries<TestProduct>().Skip(before).First(query => query.Filters.Count == 1);
            Assert.Equal(0, firstSearchQuery.Skip);
        });
    }

    [Fact]
    public void ClearingTheSearch_RemovesItsFilter_AndShowsAllRowsAgain()
    {
        var provider = ProviderFor(catalog);
        var cut = RenderList(provider, quickSearchProperties: ["Name"]);
        Type(cut, "gam");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tr.base-list-row:not([row-type])")));

        cut.FindComponents<FluentButton>().Single(button => button.Instance.Class == "base-list-quick-search-clear").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(provider.ReceivedQueries<TestProduct>().Last().Filters);
            Assert.Equal(3, cut.FindAll("tr.base-list-row:not([row-type])").Count);
        });
    }

    [Fact]
    public void ABlankSearch_IsNotSentAsAFilter()
    {
        var provider = ProviderFor(catalog);
        var cut = RenderList(provider, quickSearchProperties: ["Name"]);
        cut.WaitForAssertion(() => Assert.NotEmpty(provider.ReceivedQueries<TestProduct>()));
        var before = provider.ReceivedQueries<TestProduct>().Count();

        Type(cut, "   ");

        Assert.Equal(before, provider.ReceivedQueries<TestProduct>().Count());
    }

    [Fact]
    public void APropertyTheUserMayNotRead_IsLeftOutOfTheSearch()
    {
        var provider = ProviderFor(catalog);
        var cut = RenderList(provider, quickSearchProperties: ["Name", "InternalNotes"]);

        Type(cut, "alpha");

        cut.WaitForAssertion(() => Assert.Equal("Name", Assert.Single(provider.ReceivedQueries<TestProduct>().Last().Filters).PropertyName));
    }

    [Fact]
    public void AListWhoseNamedPropertiesAreAllUnreadable_ShowsNoBox()
    {
        var cut = RenderList(ProviderFor(catalog), quickSearchProperties: ["InternalNotes", "Price", "DoesNotExist"]);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(BoxSelector)));
    }

    [Fact]
    public void TheDebounceOfTheList_DelaysTheLoad()
    {
        var provider = ProviderFor(catalog);
        var cut = RenderList(provider, quickSearchProperties: ["Name"], debounceMilliseconds: 150);
        cut.WaitForAssertion(() => Assert.NotEmpty(provider.ReceivedQueries<TestProduct>()));
        var before = provider.ReceivedQueries<TestProduct>().Count();
        var input = cut.FindComponent<FluentTextInput>();

        var typing = cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync("gam"));

        Assert.Equal(before, provider.ReceivedQueries<TestProduct>().Count());
        typing.GetAwaiter().GetResult();
        cut.WaitForAssertion(() => Assert.Equal("gam", Assert.Single(provider.ReceivedQueries<TestProduct>().Last().Filters).Value));
    }

    [Fact]
    public void OnATileLayout_TheBoxIsARowOfItsOwn_AboveTheCards()
    {
        var provider = ProviderFor(catalog);
        var cut = RenderList(provider, quickSearchProperties: ["Name"], mobileLayout: BaseListMobileLayout.Always);

        Assert.Single(cut.FindAll(".base-list-search-row " + BoxSelector));
        Assert.Empty(cut.FindAll(".base-list-header " + BoxSelector));

        Type(cut, "gam");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".base-list-card")));
    }

    [Fact]
    public void OnATileLayout_AListWithoutNamedProperties_ShowsNoBox()
    {
        var cut = RenderList(ProviderFor(catalog), mobileLayout: BaseListMobileLayout.Always);

        Assert.Empty(cut.FindAll(BoxSelector));
        Assert.Empty(cut.FindAll(".base-list-search-row"));
    }

    private static void Type<TModel>(IRenderedComponent<BaseList<TModel>> cut, string text) where TModel : class, new() =>
        cut.FindComponent<FluentTextInput>().SetValue<string?>(text);

    private static IBaseDataProvider<TestProduct> ProviderFor(List<TestProduct> products)
    {
        var inMemory = new InMemoryBaseDataProvider<TestProduct>(() => products);
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => inMemory.GetListAsync(call.Arg<BaseQuery>()));
        return provider;
    }

    private static IBaseDataProvider<TestPrinting> PrintingProvider()
    {
        var provider = Substitute.For<IBaseDataProvider<TestPrinting>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestPrinting, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestPrinting> { Items = [new TestPrinting { Id = Guid.NewGuid(), Code = "X1" }], TotalCount = 1 });
        return provider;
    }

    private static IBaseDataProvider<TestProduct> PagedProvider(int total)
    {
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<BaseQuery>();
                var count = Math.Max(0, Math.Min(query.Take, total - query.Skip));
                return new BaseQueryResult<TestProduct>
                {
                    Items = [.. Enumerable.Range(0, count).Select(_ => new TestProduct { Id = Guid.NewGuid(), Name = "P" })],
                    TotalCount = total
                };
            });
        return provider;
    }

    private static void WaitForQueriesToSettle<TModel>(IBaseDataProvider<TModel> provider) where TModel : class
    {
        var last = -1;
        var stable = 0;

        for (var attempt = 0; attempt < 100 && stable < 6; attempt++)
        {
            Thread.Sleep(50);
            var count = provider.ReceivedQueries<TModel>().Count();
            stable = count == last && count > 0 ? stable + 1 : 0;
            last = count;
        }
    }

    private IRenderedComponent<BaseList<TestProduct>> RenderList(
        IBaseDataProvider<TestProduct> provider,
        List<string>? quickSearchProperties = null,
        bool? showQuickSearch = null,
        BaseListConfiguration<TestProduct>? configuration = null,
        List<FilterDescriptor>? additionalFilters = null,
        BaseListMobileLayout mobileLayout = BaseListMobileLayout.Never,
        int pageSize = 50,
        int debounceMilliseconds = 0,
        ListFilterDefaults? filterDefaults = null,
        string secondColumn = nameof(TestProduct.Description))
    {
        SetUpEnvironment();
        Services.AddSingleton(provider);

        return Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.AllowEdit, false)
            .Add(p => p.AllowDelete, false)
            .Add(p => p.PageSize, pageSize)
            .Add(p => p.MobileLayout, mobileLayout)
            .Add(p => p.QuickSearchProperties, quickSearchProperties)
            .Add(p => p.ShowQuickSearch, showQuickSearch)
            .Add(p => p.QuickSearchDebounceMilliseconds, debounceMilliseconds)
            .Add(p => p.Configuration, configuration)
            .Add(p => p.AdditionalFilters, additionalFilters)
            .Add(p => p.FilterDefaults, filterDefaults)
            .Add(p => p.ChildContent, BuildColumns<TestProduct>(nameof(TestProduct.Name), secondColumn)));
    }

    private IRenderedComponent<BaseList<TestPrinting>> RenderPrintingList(IBaseDataProvider<TestPrinting> provider, bool? showQuickSearch)
    {
        SetUpEnvironment();
        Services.AddSingleton(provider);

        return Render<BaseList<TestPrinting>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.MobileLayout, BaseListMobileLayout.Never)
            .Add(p => p.ShowQuickSearch, showQuickSearch)
            .Add(p => p.QuickSearchDebounceMilliseconds, 0)
            .Add(p => p.ChildContent, BuildColumns<TestPrinting>(nameof(TestPrinting.Code))));
    }

    private void SetUpEnvironment()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
        Services.AddLocalization();
    }

    private static RenderFragment BuildColumns<TModel>(string firstPropertyName, string? secondPropertyName = null) where TModel : class, new() =>
        builder =>
        {
            builder.OpenComponent<BaseColumn<TModel>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TModel>.Property), PropertyOf<TModel>(firstPropertyName));
            builder.CloseComponent();

            if (secondPropertyName is null)
                return;

            builder.OpenComponent<BaseColumn<TModel>>(2);
            builder.AddComponentParameter(3, nameof(BaseColumn<TModel>.Property), PropertyOf<TModel>(secondPropertyName));
            builder.CloseComponent();
        };

    private static Expression<Func<TModel, object?>> PropertyOf<TModel>(string propertyName)
    {
        var parameter = Expression.Parameter(typeof(TModel), "item");
        var body = Expression.Convert(Expression.Property(parameter, propertyName), typeof(object));

        return Expression.Lambda<Func<TModel, object?>>(body, parameter);
    }
}
