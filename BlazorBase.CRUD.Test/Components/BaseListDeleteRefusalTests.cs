using System.Linq.Expressions;
using System.Reflection;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A rule that refuses a delete, such as a category that is still in use, reaches the list as a validation
/// error. It is shown above the list instead of escaping as an unhandled error, and the row stays.
/// </summary>
public class BaseListDeleteRefusalTests : BunitTestContextBase
{
    private const string RefusalMessage = "The order is still in use and cannot be deleted";

    [Fact]
    public async Task DeleteRefusedByValidation_ShowsTheMessageAboveTheList()
    {
        var order = new TestOrder { Id = Guid.NewGuid(), CustomerName = "A", Total = 1m };
        var provider = ProviderRefusingDelete(order);
        var cut = RenderList(provider);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));

        await cut.InvokeAsync(() => DeleteAsync(cut.Instance, order));
        cut.Render();

        cut.WaitForAssertion(() => Assert.Contains(RefusalMessage, cut.Find(".base-list-delete-error").TextContent));
        await provider.Received(1).DeleteAsync(order.Id, Arg.Any<CancellationToken>());
        Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row"));
    }

    [Fact]
    public async Task SuccessfulDelete_ShowsNoMessage()
    {
        var order = new TestOrder { Id = Guid.NewGuid(), CustomerName = "A", Total = 1m };
        var provider = ProviderReturning(order);
        var cut = RenderList(provider);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));

        await cut.InvokeAsync(() => DeleteAsync(cut.Instance, order));
        cut.Render();

        Assert.Empty(cut.FindAll(".base-list-delete-error"));
        await provider.Received(1).DeleteAsync(order.Id, Arg.Any<CancellationToken>());
    }

    private static Task DeleteAsync(BaseList<TestOrder> list, TestOrder order)
    {
        var method = typeof(BaseList<TestOrder>).GetMethod("OnDeleteItemAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(list, [order])!;
    }

    private static IBaseDataProvider<TestOrder> ProviderRefusingDelete(TestOrder order)
    {
        var provider = ProviderReturning(order);
        provider.DeleteAsync(order.Id, Arg.Any<CancellationToken>()).ThrowsAsync(new BaseValidationException(RefusalMessage));
        return provider;
    }

    private static IBaseDataProvider<TestOrder> ProviderReturning(TestOrder order)
    {
        var provider = Substitute.For<IBaseDataProvider<TestOrder>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestOrder, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestOrder> { Items = [order], TotalCount = 1 });
        return provider;
    }

    private IRenderedComponent<BaseList<TestOrder>> RenderList(IBaseDataProvider<TestOrder> provider)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddSingleton(provider);

        return Render<BaseList<TestOrder>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
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
