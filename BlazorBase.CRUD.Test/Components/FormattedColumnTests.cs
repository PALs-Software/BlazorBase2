using System.Globalization;
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
/// A <c>BaseColumn</c> with <c>Format</c> set is rendered dynamically against the type-erased
/// <c>Expression&lt;Func&lt;TModel, object?&gt;&gt;</c> every column carries, so it cannot flow
/// through FluentUI's <c>PropertyColumn</c> (whose generic <c>TProp</c> would be inferred as
/// <c>object</c>, which does not implement <c>IFormattable</c> and throws). These pin down the
/// fallback <c>TemplateColumn</c> rendering path that formats the value manually instead.
/// </summary>
public class FormattedColumnTests : BunitTestContextBase
{
    [Fact]
    public void ColumnWithFormat_RendersFormattedValue_InsteadOfThrowing()
    {
        AuthorizeAdmin();
        var order = new TestOrder { Id = Guid.NewGuid(), CustomerName = "A", Total = 12.5m };
        var provider = ProviderReturning(order);
        Services.AddSingleton(provider);

        var cut = RenderList(provider, o => o.Total, "F2", sortable: false);

        var expected = string.Format(CultureInfo.CurrentCulture, "{0:F2}", order.Total);
        cut.WaitForAssertion(() => Assert.Contains(expected, cut.Markup));
    }

    [Fact]
    public void SortableColumnWithFormat_RendersFormattedValue_InsteadOfThrowing()
    {
        AuthorizeAdmin();
        var order = new TestOrder { Id = Guid.NewGuid(), CustomerName = "A", Total = 12.5m };
        var provider = ProviderReturning(order);
        Services.AddSingleton(provider);

        var cut = RenderList(provider, o => o.Total, "F2", sortable: true);

        var expected = string.Format(CultureInfo.CurrentCulture, "{0:F2}", order.Total);
        cut.WaitForAssertion(() => Assert.Contains(expected, cut.Markup));
    }

    private void AuthorizeAdmin()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
    }

    private static IBaseDataProvider<TestOrder> ProviderReturning(params TestOrder[] items)
    {
        var provider = Substitute.For<IBaseDataProvider<TestOrder>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestOrder, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestOrder> { Items = [.. items], TotalCount = items.Length });
        return provider;
    }

    private IRenderedComponent<BaseList<TestOrder>> RenderList(
        IBaseDataProvider<TestOrder> provider,
        Expression<Func<TestOrder, object?>> columnProperty,
        string format,
        bool sortable)
    {
        return Render<BaseList<TestOrder>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, BuildColumn(columnProperty, format, sortable)));
    }

    private static RenderFragment BuildColumn(Expression<Func<TestOrder, object?>> columnProperty, string format, bool sortable)
    {
        return builder =>
        {
            builder.OpenComponent<BaseColumn<TestOrder>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestOrder>.Property), columnProperty);
            builder.AddComponentParameter(2, nameof(BaseColumn<TestOrder>.Format), format);
            builder.AddComponentParameter(3, nameof(BaseColumn<TestOrder>.Sortable), sortable);
            builder.CloseComponent();
        };
    }
}
