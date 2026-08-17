using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Extensions;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.CustomProperties;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

[Collection(CustomPropertyResolutionCollection.Name)]
public class CustomPropertyDisplayTests : BunitTestContextBase
{
    [Fact]
    public void RegisteredCustomDisplay_RendersInCell_WhenCanHandleMatches()
    {
        AuthorizeAdmin();
        var provider = ProviderReturning(new TestProduct { Id = Guid.NewGuid(), IsActive = true });
        Services.AddSingleton(provider);
        Services.AddBlazorBaseCustomDisplay<SampleStatusDisplay>();

        var cut = RenderList(provider, p => p.IsActive);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("span.sample-status-badge")));
        Assert.Single(cut.FindComponents<SampleStatusDisplay>());
        Assert.Contains("ON", cut.Markup);
    }

    [Fact]
    public void NoCustomDisplay_FallsBackToDefaultCell()
    {
        AuthorizeAdmin();
        var provider = ProviderReturning(new TestProduct { Id = Guid.NewGuid(), Name = "P", IsActive = true });
        Services.AddSingleton(provider);

        var cut = RenderList(provider, p => p.IsActive);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr")));
        Assert.Empty(cut.FindComponents<SampleStatusDisplay>());
    }

    private void AuthorizeAdmin()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
    }

    private static IBaseDataProvider<TestProduct> ProviderReturning(params TestProduct[] items)
    {
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct> { Items = [.. items], TotalCount = items.Length });
        return provider;
    }

    private IRenderedComponent<BaseList<TestProduct>> RenderList(
        IBaseDataProvider<TestProduct> provider,
        Expression<Func<TestProduct, object?>> columnProperty)
    {
        return Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, BuildColumn(columnProperty)));
    }

    private static RenderFragment BuildColumn(Expression<Func<TestProduct, object?>> columnProperty)
    {
        return builder =>
        {
            builder.OpenComponent<BaseColumn<TestProduct>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), columnProperty);
            builder.AddComponentParameter(2, nameof(BaseColumn<TestProduct>.Sortable), false);
            builder.CloseComponent();
        };
    }
}
