using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// After an edit only the entry that changed is read again: the provider is asked for it by id and not for a list, and
/// the row shows the new values at the position it had. What cannot be refreshed that way reloads the visible window.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListRowRefreshTests : BunitTestContextBase
{
    private readonly TestProduct Alpha = new() { Id = Guid.NewGuid(), Name = "Alpha" };
    private readonly TestProduct Bravo = new() { Id = Guid.NewGuid(), Name = "Bravo" };
    private readonly TestProduct Charlie = new() { Id = Guid.NewGuid(), Name = "Charlie" };

    [Fact]
    public async Task RefreshingAnItem_ReadsItById_AndShowsItsNewValueInPlace()
    {
        var provider = Provider(byId: _ => new TestProduct { Id = Bravo.Id, Name = "Bravo edited" });
        var cut = await RenderListAsync(provider);
        var listCallsBefore = GetListCalls(provider);

        await cut.InvokeAsync(() => cut.Instance.RefreshItemAsync(Bravo.Id));

        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo edited", "Charlie"], RowNames(cut)));
        Assert.Equal(listCallsBefore, GetListCalls(provider));
        var byId = Assert.Single(GetByIdCalls(provider));
        Assert.Equal(Bravo.Id, byId.Id);
    }

    [Fact]
    public async Task RefreshingAnItem_AsksForTheColumnsTheListShows()
    {
        var provider = Provider(byId: _ => new TestProduct { Id = Bravo.Id, Name = "Bravo edited" });
        var cut = await RenderListAsync(provider);

        await cut.InvokeAsync(() => cut.Instance.RefreshItemAsync(Bravo.Id));

        var select = Assert.Single(GetByIdCalls(provider)).Select;
        Assert.NotNull(select);
        Assert.Contains("Id", select, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Name", select, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnItemTheProviderNoLongerFinds_ReloadsTheVisibleWindow()
    {
        var provider = Provider(byId: _ => null);
        var cut = await RenderListAsync(provider);
        var listCallsBefore = GetListCalls(provider);

        await cut.InvokeAsync(() => cut.Instance.RefreshItemAsync(Bravo.Id));

        Assert.True(GetListCalls(provider) > listCallsBefore);
    }

    [Fact]
    public async Task AnItemThatIsNotHeldByTheList_ReloadsTheVisibleWindow()
    {
        var elsewhere = Guid.NewGuid();
        var provider = Provider(byId: _ => new TestProduct { Id = elsewhere, Name = "Elsewhere" });
        var cut = await RenderListAsync(provider);
        var listCallsBefore = GetListCalls(provider);

        await cut.InvokeAsync(() => cut.Instance.RefreshItemAsync(elsewhere));

        Assert.True(GetListCalls(provider) > listCallsBefore);
        Assert.Equal(1, GetByIdCalls(provider).Count);
    }

    [Fact]
    public async Task AProviderThatFailsToRead_ReloadsTheVisibleWindow()
    {
        var provider = Provider(byId: _ => throw new HttpRequestException("offline"));
        var cut = await RenderListAsync(provider);
        var listCallsBefore = GetListCalls(provider);

        await cut.InvokeAsync(() => cut.Instance.RefreshItemAsync(Bravo.Id));

        Assert.True(GetListCalls(provider) > listCallsBefore);
    }

    [Fact]
    public async Task RefreshingTwoItemsInARow_KeepsBothChanges()
    {
        var provider = Provider(byId: id => new TestProduct { Id = (Guid)id, Name = id.Equals(Alpha.Id) ? "Alpha edited" : "Charlie edited" });
        var cut = await RenderListAsync(provider);

        await cut.InvokeAsync(() => cut.Instance.RefreshItemAsync(Alpha.Id));
        await cut.InvokeAsync(() => cut.Instance.RefreshItemAsync(Charlie.Id));

        cut.WaitForAssertion(() => Assert.Equal(["Alpha edited", "Bravo", "Charlie edited"], RowNames(cut)));
    }

    private static List<string> RowNames(IRenderedComponent<BaseList<TestProduct>> cut)
        => [.. cut.FindAll("tr.fluent-data-grid-row:not([row-type]) td").Select(cell => cell.TextContent.Trim()).Where(text => text.Length > 0)];

    private static int GetListCalls(IBaseDataProvider<TestProduct> provider)
        => provider.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IBaseDataProvider<TestProduct>.GetListAsync));

    private static List<(object Id, string[]? Select)> GetByIdCalls(IBaseDataProvider<TestProduct> provider)
        => [.. provider.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IBaseDataProvider<TestProduct>.GetByIdAsync))
            .Select(call => (call.GetArguments()[0]!, (call.GetArguments()[1] as IEnumerable<string>)?.ToArray()))];

    private IBaseDataProvider<TestProduct> Provider(Func<object, TestProduct?> byId)
    {
        var items = new List<TestProduct> { Alpha, Bravo, Charlie };
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_ => new BaseQueryResult<TestProduct> { Items = [.. items], TotalCount = items.Count });
        provider.GetByIdAsync(Arg.Any<object>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<Expression<Func<TestProduct, bool>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(byId(call.Arg<object>())));
        return provider;
    }

    private async Task<IRenderedComponent<BaseList<TestProduct>>> RenderListAsync(IBaseDataProvider<TestProduct> provider)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, BuildColumn(item => item.Name)));
        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo", "Charlie"], RowNames(cut)));
        await Task.Delay(200);

        return cut;
    }

    private static RenderFragment BuildColumn(Expression<Func<TestProduct, object?>> columnProperty)
    {
        return builder =>
        {
            builder.OpenComponent<BaseColumn<TestProduct>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), columnProperty);
            builder.CloseComponent();
        };
    }
}
