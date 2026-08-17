using System.Linq.Expressions;
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
/// What a column puts on screen: a boolean reads the way the filter panel already offers it rather
/// than as a raw CLR value, and the width a caller asked for actually reaches the grid.
/// </summary>
/// <remarks>
/// Joins the custom-property collection because <c>TestProduct.IsActive</c> is exactly what
/// <c>SampleStatusDisplay</c> claims: run in parallel, that registration leaks through the static
/// resolution cache and the boolean cell renders as a custom display instead.
/// </remarks>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListColumnRenderingTests : BunitTestContextBase
{
    [Fact]
    public void BooleanColumn_RendersTheLocalizedYes_InsteadOfTheClrValue()
    {
        var cut = RenderList(product => product.IsActive, width: null, isActive: true);

        cut.WaitForAssertion(() => Assert.Contains(Framework["BoolTrue"].Value, cut.Markup));
        Assert.DoesNotContain(">True<", cut.Markup);
    }

    [Fact]
    public void BooleanColumn_RendersTheLocalizedNo_InsteadOfTheClrValue()
    {
        var cut = RenderList(product => product.IsActive, width: null, isActive: false);

        cut.WaitForAssertion(() => Assert.Contains(Framework["BoolFalse"].Value, cut.Markup));
        Assert.DoesNotContain(">False<", cut.Markup);
    }

    [Fact]
    public void ColumnWidth_ReachesTheRenderedCells()
    {
        var cut = RenderList(product => product.Name, width: "180px", isActive: true);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));
        Assert.Contains("180px", cut.Markup);
    }

    [Fact]
    public void ColumnWithoutWidth_IsMarkedFlexible_SoTheStylesheetCanKeepItReadable()
    {
        var cut = RenderList(product => product.Name, width: null, isActive: true);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-column-flexible")));
    }

    [Fact]
    public void ColumnWithWidth_IsNotMarkedFlexible()
    {
        var cut = RenderList(product => product.Name, width: "180px", isActive: true);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));
        Assert.Empty(cut.FindAll(".base-list-column-flexible"));
    }

    private IStringLocalizer Framework => Services.GetRequiredService<IStringLocalizerFactory>()
        .Create(typeof(BlazorBaseCrudResources));

    private IRenderedComponent<BaseList<TestProduct>> RenderList(
        Expression<Func<TestProduct, object?>> columnProperty,
        string? width,
        bool isActive)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();

        var product = new TestProduct { Id = Guid.NewGuid(), Name = "Widget", IsActive = isActive };
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct> { Items = [product], TotalCount = 1 });

        Services.AddSingleton(provider);

        return Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, BuildColumn(columnProperty, width)));
    }

    private static RenderFragment BuildColumn(Expression<Func<TestProduct, object?>> columnProperty, string? width)
    {
        return builder =>
        {
            builder.OpenComponent<BaseColumn<TestProduct>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), columnProperty);
            builder.AddComponentParameter(2, nameof(BaseColumn<TestProduct>.Width), width);
            builder.CloseComponent();
        };
    }
}
