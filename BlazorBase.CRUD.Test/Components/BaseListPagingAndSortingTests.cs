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
/// A list used to load its first fifty rows and never any more, so every row past the fiftieth was unreachable. It is
/// virtualized now: the rows near the visible part of the page are fetched from the data provider in aligned chunks of
/// <c>PageSize</c> rows, each chunk once, and the order the list shows is the one the provider was asked for.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListPagingAndSortingTests : BunitTestContextBase
{
    [Fact]
    public void FirstChunk_IsRequestedWithThePageSize_AndTheTotalIsShown()
    {
        var provider = PagedProvider(total: 5);

        var cut = RenderList<TestOrder>(provider, pageSize: 10);

        cut.WaitForAssertion(() => Assert.Equal(Total(5), cut.Find(".base-list-count").TextContent.Trim()));
        var query = provider.ReceivedQueries<TestOrder>().First();
        Assert.Equal(0, query.Skip);
        Assert.Equal(10, query.Take);
    }

    [Fact]
    public void ARangeAcrossAChunkBoundary_IsFetchedInOneRequestOfAlignedChunks()
    {
        var provider = PagedProvider(total: 1000);
        var cut = RenderList<TestOrder>(provider, pageSize: 50);
        WaitForQueriesToSettle(provider);
        var before = provider.ReceivedQueries<TestOrder>().Count();

        var items = cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(620, 60, CancellationToken.None, debounce: false)).GetAwaiter().GetResult();

        Assert.Equal(60, items.Count);
        var fetch = provider.ReceivedQueries<TestOrder>().Skip(before).First();
        Assert.Equal(600, fetch.Skip);
        Assert.Equal(100, fetch.Take);
    }

    [Fact]
    public void ARangeThatIsHeld_IsNotFetchedAgain()
    {
        var provider = PagedProvider(total: 1000);
        var cut = RenderList<TestOrder>(provider, pageSize: 50);
        WaitForQueriesToSettle(provider);
        cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(620, 60, CancellationToken.None, debounce: false)).GetAwaiter().GetResult();
        WaitForQueriesToSettle(provider);
        var before = provider.ReceivedQueries<TestOrder>().Count();

        var again = cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(630, 20, CancellationToken.None, debounce: false)).GetAwaiter().GetResult();

        Assert.Equal(20, again.Count);
        Assert.Equal(before, provider.ReceivedQueries<TestOrder>().Count());
    }

    [Fact]
    public void RangeRequests_KeepTheActiveFilter()
    {
        var provider = PagedProvider(total: 1000);
        var filters = new List<FilterDescriptor> { new() { PropertyName = "CustomerName", Operator = FilterOperator.Contains, Value = "A" } };
        var cut = RenderList<TestOrder>(provider, pageSize: 50, additionalFilters: filters);
        WaitForQueriesToSettle(provider);
        var before = provider.ReceivedQueries<TestOrder>().Count();

        cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(700, 10, CancellationToken.None, debounce: false)).GetAwaiter().GetResult();

        Assert.All(provider.ReceivedQueries<TestOrder>(), query => Assert.Single(query.Filters));
        Assert.True(provider.ReceivedQueries<TestOrder>().Count() > before);
    }

    [Fact]
    public void TwoRequestsForTheSameChunk_ShareOneFetch()
    {
        var provider = PagedProvider(total: 1000, delayMilliseconds: 150);
        var cut = RenderList<TestOrder>(provider, pageSize: 50);
        WaitForQueriesToSettle(provider);
        var before = provider.ReceivedQueries<TestOrder>().Count();

        var first = cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(750, 20, CancellationToken.None, debounce: false));
        var second = cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(760, 20, CancellationToken.None, debounce: false));
        Task.WaitAll(first, second);

        Assert.Equal(1, provider.ReceivedQueries<TestOrder>().Skip(before).Count(query => query.Skip == 750));
    }

    [Fact]
    public void ARequestThatWasCancelledBeforeItFetched_DoesNotCallTheProvider()
    {
        var provider = PagedProvider(total: 1000);
        var cut = RenderList<TestOrder>(provider, pageSize: 50);
        WaitForQueriesToSettle(provider);
        var before = provider.ReceivedQueries<TestOrder>().Count();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var read = cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(800, 20, cancellation.Token, debounce: true));

        Assert.ThrowsAny<OperationCanceledException>(() => read.GetAwaiter().GetResult());
        Assert.DoesNotContain(provider.ReceivedQueries<TestOrder>().Skip(before), query => query.Skip == 800);
    }

    [Fact]
    public void AProviderThatIgnoresPaging_IsAskedOnce_AndServedByIndex()
    {
        var all = Enumerable.Range(0, 120).Select(index => new TestOrder { Id = Guid.NewGuid(), CustomerName = "C" + index }).ToList();
        var provider = Substitute.For<IBaseDataProvider<TestOrder>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestOrder, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_ => new BaseQueryResult<TestOrder> { Items = [.. all], TotalCount = all.Count });
        var cut = RenderList<TestOrder>(provider, pageSize: 50);
        WaitForQueriesToSettle(provider);
        var before = provider.ReceivedQueries<TestOrder>().Count();

        var items = cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(100, 20, CancellationToken.None, debounce: false)).GetAwaiter().GetResult();

        Assert.Equal(all.Skip(100).Take(20), items);
        Assert.Equal(before, provider.ReceivedQueries<TestOrder>().Count());
    }

    [Fact]
    public void Refresh_BringsBackTheChunksThatAreOnScreen_NotTheFirstOnes()
    {
        var provider = PagedProvider(total: 1000);
        var cut = RenderList<TestOrder>(provider, pageSize: 50);
        WaitForQueriesToSettle(provider);
        cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(800, 20, CancellationToken.None, debounce: false)).GetAwaiter().GetResult();
        WaitForQueriesToSettle(provider);
        var before = provider.ReceivedQueries<TestOrder>().Count();

        cut.InvokeAsync(() => cut.Instance.RefreshAsync()).GetAwaiter().GetResult();

        var refresh = provider.ReceivedQueries<TestOrder>().Skip(before).First();
        Assert.Equal(800, refresh.Skip);
        Assert.Equal(50, refresh.Take);
    }

    [Fact]
    public void Refresh_AfterTheListShrankBelowTheViewedPart_StartsAtTheTopAgain()
    {
        var total = 1000;
        var provider = Substitute.For<IBaseDataProvider<TestOrder>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestOrder, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<BaseQuery>();
                var count = Math.Max(0, Math.Min(query.Take, total - query.Skip));
                return new BaseQueryResult<TestOrder> { Items = [.. Enumerable.Range(0, count).Select(_ => new TestOrder())], TotalCount = total };
            });
        var cut = RenderList<TestOrder>(provider, pageSize: 50);
        WaitForQueriesToSettle(provider);
        cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(800, 20, CancellationToken.None, debounce: false)).GetAwaiter().GetResult();
        WaitForQueriesToSettle(provider);
        total = 40;
        var before = provider.ReceivedQueries<TestOrder>().Count();

        cut.InvokeAsync(() => cut.Instance.RefreshAsync()).GetAwaiter().GetResult();

        Assert.Equal(40, cut.InvokeAsync(() => cut.Instance.ReadRangeAsync(0, 50, CancellationToken.None, debounce: false)).GetAwaiter().GetResult().Count);
        Assert.Contains(provider.ReceivedQueries<TestOrder>().Skip(before), query => query.Skip == 0);
    }

    [Fact]
    public void ListThatFitsOnOnePage_ShowsItsRows()
    {
        var provider = PagedProvider(total: 2);

        var cut = RenderList<TestOrder>(provider, pageSize: 2);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));
        Assert.Empty(cut.FindAll(".base-list-load-more"));
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

    private IBaseDataProvider<TModel> PagedProvider<TModel>(int total, int delayMilliseconds = 0) where TModel : class, new()
    {
        var provider = Substitute.For<IBaseDataProvider<TModel>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TModel, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                if (delayMilliseconds > 0)
                    await Task.Delay(delayMilliseconds);

                var query = call.Arg<BaseQuery>();
                var count = Math.Max(0, Math.Min(query.Take, total - query.Skip));
                var items = Enumerable.Range(0, count).Select(_ => new TModel()).ToList();
                return new BaseQueryResult<TModel> { Items = items, TotalCount = total };
            });
        return provider;
    }

    private IBaseDataProvider<TestOrder> PagedProvider(int total, int delayMilliseconds = 0) => PagedProvider<TestOrder>(total, delayMilliseconds);

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

    private string Total(int total) => string.Format(
        Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources))["TotalEntries"].Value, total);

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
