using System.Linq.Expressions;
using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A list used to load its first fifty rows and never any more: nothing ever called the load-more method, so
/// every row past the fiftieth was unreachable. It also asked for them in no particular order.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListPagingAndSortingTests : BunitTestContextBase
{
    [Fact]
    public void FirstPage_RequestsOnlyOnePageAndShowsHowMuchIsLoaded()
    {
        var provider = PagedProvider(total: 5);

        var cut = RenderList<TestOrder>(provider, pageSize: 2);

        cut.WaitForAssertion(() => Assert.Equal(Loaded(2, 5), cut.Find(".base-list-count").TextContent.Trim()));
        var query = provider.ReceivedQueries<TestOrder>().First();
        Assert.Equal(0, query.Skip);
        Assert.Equal(2, query.Take);
    }

    [Fact]
    public void LoadMore_RequestsTheNextPageAfterWhatIsLoaded_UntilEverythingIsLoaded()
    {
        var provider = PagedProvider(total: 5);
        var cut = RenderList<TestOrder>(provider, pageSize: 2);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-load-more")));

        cut.Find(".base-list-load-more").Click();
        cut.WaitForAssertion(() => Assert.Equal(Loaded(4, 5), cut.Find(".base-list-count").TextContent.Trim()));
        cut.Find(".base-list-load-more").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-list-footer")));
        Assert.Equal([0, 2, 4], provider.ReceivedQueries<TestOrder>().Select(query => query.Skip));
    }

    [Fact]
    public void LoadMore_KeepsTheActiveFilter()
    {
        var provider = PagedProvider(total: 5);
        var filters = new List<FilterDescriptor> { new() { PropertyName = "CustomerName", Operator = FilterOperator.Contains, Value = "A" } };
        var cut = RenderList<TestOrder>(provider, pageSize: 2, additionalFilters: filters);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-load-more")));

        cut.Find(".base-list-load-more").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, provider.ReceivedQueries<TestOrder>().Count()));
        Assert.All(provider.ReceivedQueries<TestOrder>(), query => Assert.Single(query.Filters));
    }

    [Fact]
    public void Refresh_KeepsAsManyRowsAsWereAlreadyLoaded_SoAPollDoesNotDiscardTheScrolledPages()
    {
        var provider = PagedProvider(total: 5);
        var cut = RenderList<TestOrder>(provider, pageSize: 2);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-load-more")));
        cut.Find(".base-list-load-more").Click();
        cut.WaitForAssertion(() => Assert.Equal(Loaded(4, 5), cut.Find(".base-list-count").TextContent.Trim()));

        cut.InvokeAsync(() => cut.Instance.RefreshAsync()).GetAwaiter().GetResult();

        var refresh = provider.ReceivedQueries<TestOrder>().Last();
        Assert.Equal(0, refresh.Skip);
        Assert.Equal(4, refresh.Take);
    }

    [Fact]
    public void ListThatFitsOnOnePage_ShowsNoLoadMore()
    {
        var provider = PagedProvider(total: 2);

        var cut = RenderList<TestOrder>(provider, pageSize: 2);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));
        Assert.Empty(cut.FindAll(".base-list-footer"));
    }

    [Fact]
    public void WithoutADeclaredSort_NoSortIsSent_SoTheProviderFallsBackToThePrimaryKey()
    {
        var provider = PagedProvider(total: 1);

        RenderList<TestOrder>(provider, pageSize: 2).WaitForAssertion(
            () => Assert.NotEmpty(provider.ReceivedQueries<TestOrder>()));

        Assert.Empty(provider.ReceivedQueries<TestOrder>().First().Sorts);
    }

    [Fact]
    public void TheModelsDeclaredDefaultSort_IsSentInOrder()
    {
        var provider = PagedProvider<SortedNote>(total: 1);

        RenderList<SortedNote>(provider, pageSize: 2).WaitForAssertion(
            () => Assert.NotEmpty(provider.ReceivedQueries<SortedNote>()));

        var sorts = provider.ReceivedQueries<SortedNote>().First().Sorts;
        Assert.Equal(["Priority", "Title"], sorts.Select(sort => sort.PropertyName));
        Assert.Equal([SortDirection.Descending, SortDirection.Ascending], sorts.Select(sort => sort.Direction));
    }

    [Fact]
    public void ADefaultSortOnTheList_BeatsTheModelsDeclaration()
    {
        var provider = PagedProvider<SortedNote>(total: 1);
        List<SortDescriptor> listSorts = [new() { PropertyName = "Title", Direction = SortDirection.Descending }];

        RenderList<SortedNote>(provider, pageSize: 2, defaultSorts: listSorts).WaitForAssertion(
            () => Assert.NotEmpty(provider.ReceivedQueries<SortedNote>()));

        var sorts = provider.ReceivedQueries<SortedNote>().First().Sorts;
        Assert.Equal("Title", Assert.Single(sorts).PropertyName);
    }

    [Fact]
    public void ADefaultSortInTheListConfiguration_BeatsTheModelsDeclaration()
    {
        var provider = PagedProvider<SortedNote>(total: 1);
        var configuration = new BlazorBase.CRUD.Configuration.BaseListBuilder<SortedNote>()
            .Column(note => note.Title)
            .DefaultSort(note => note.Id, descending: true)
            .Build();

        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
        Services.AddLocalization();

        var cut = Render<BaseList<SortedNote>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.Configuration, configuration));
        cut.WaitForAssertion(() => Assert.NotEmpty(provider.ReceivedQueries<SortedNote>()));

        var sort = Assert.Single(provider.ReceivedQueries<SortedNote>().First().Sorts);
        Assert.Equal("Id", sort.PropertyName);
        Assert.Equal(SortDirection.Descending, sort.Direction);
    }

    private IBaseDataProvider<TModel> PagedProvider<TModel>(int total) where TModel : class, new()
    {
        var provider = Substitute.For<IBaseDataProvider<TModel>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TModel, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<BaseQuery>();
                var count = Math.Max(0, Math.Min(query.Take, total - query.Skip));
                var items = Enumerable.Range(0, count).Select(_ => new TModel()).ToList();
                return new BaseQueryResult<TModel> { Items = items, TotalCount = total };
            });
        return provider;
    }

    private IBaseDataProvider<TestOrder> PagedProvider(int total) => PagedProvider<TestOrder>(total);

    private string Loaded(int loaded, int total) => string.Format(
        Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources))["LoadedOfTotal"].Value, loaded, total);

    private IRenderedComponent<BaseList<TModel>> RenderList<TModel>(
        IBaseDataProvider<TModel> provider,
        int pageSize,
        List<FilterDescriptor>? additionalFilters = null,
        List<SortDescriptor>? defaultSorts = null)
        where TModel : class, new()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();
        Services.AddSingleton(provider);

        return Render<BaseList<TModel>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.PageSize, pageSize)
            .Add(p => p.AdditionalFilters, additionalFilters)
            .Add(p => p.DefaultSorts, defaultSorts)
            .Add(p => p.ChildContent, BuildFirstColumn<TModel>()));
    }

    private static RenderFragment BuildFirstColumn<TModel>() where TModel : class, new()
    {
        var parameter = Expression.Parameter(typeof(TModel), "item");
        var propertyName = typeof(TModel) == typeof(TestOrder) ? nameof(TestOrder.CustomerName) : nameof(SortedNote.Title);
        var body = Expression.Convert(Expression.Property(parameter, propertyName), typeof(object));
        var property = Expression.Lambda<Func<TModel, object?>>(body, parameter);

        return builder =>
        {
            builder.OpenComponent<BaseColumn<TModel>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TModel>.Property), property);
            builder.CloseComponent();
        };
    }

    [DefaultSort(nameof(Priority), Descending = true, Order = 0)]
    [DefaultSort(nameof(Title), Order = 1)]
    public class SortedNote
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public int Priority { get; set; }
    }
}

internal static class ProviderQueryExtensions
{
    public static IEnumerable<BaseQuery> ReceivedQueries<TModel>(this IBaseDataProvider<TModel> provider) where TModel : class
        => provider.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IBaseDataProvider<TModel>.GetListAsync))
            .Select(call => (BaseQuery)call.GetArguments()[0]!);
}
