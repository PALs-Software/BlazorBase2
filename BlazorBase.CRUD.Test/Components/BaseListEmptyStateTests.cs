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
/// An empty list shows the list's own localized message and nothing else. Rendering the data grid
/// alongside it produced two competing empty states — the second one FluentUI's built-in, untranslated
/// "No data to show!" — and, worse, a virtualized grid that would not let go of a deleted row, so the
/// list claimed the row still existed after the delete had already reached the database.
/// </summary>
public class BaseListEmptyStateTests : BunitTestContextBase
{
    [Fact]
    public void EmptyList_ShowsTheConfiguredMessage()
    {
        var cut = RenderList(EmptyProvider(), emptyText: "Nothing here yet.");

        cut.WaitForAssertion(() => Assert.Equal("Nothing here yet.", cut.Find(".base-list-empty").TextContent.Trim()));
    }

    [Fact]
    public void EmptyList_RendersNoDataGrid()
    {
        var cut = RenderList(EmptyProvider(), emptyText: "Nothing here yet.");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".base-list-empty")));
        Assert.Empty(cut.FindAll("tr.fluent-data-grid-row"));
    }

    [Fact]
    public void FilledList_RendersTheDataGridAndNoEmptyMessage()
    {
        var order = new TestOrder { Id = Guid.NewGuid(), CustomerName = "A", Total = 1m };

        var cut = RenderList(ProviderReturning(order), emptyText: "Nothing here yet.");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));
        Assert.Empty(cut.FindAll(".base-list-empty"));
    }

    private static IBaseDataProvider<TestOrder> EmptyProvider() => ProviderReturning();

    private static IBaseDataProvider<TestOrder> ProviderReturning(params TestOrder[] items)
    {
        var provider = Substitute.For<IBaseDataProvider<TestOrder>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestOrder, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestOrder> { Items = [.. items], TotalCount = items.Length });
        return provider;
    }

    private IRenderedComponent<BaseList<TestOrder>> RenderList(IBaseDataProvider<TestOrder> provider, string emptyText)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddSingleton(provider);

        return Render<BaseList<TestOrder>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.EmptyText, emptyText)
            .Add(p => p.ChildContent, BuildColumn(order => order.CustomerName)));
    }

    private static RenderFragment BuildColumn(Expression<Func<TestOrder, object?>> columnProperty)
    {
        return builder =>
        {
            builder.OpenComponent<BaseColumn<TestOrder>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestOrder>.Property), columnProperty);
            builder.CloseComponent();
        };
    }
}
