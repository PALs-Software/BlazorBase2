using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
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

[Collection(CustomPropertyResolutionCollection.Name)]
public class FilterPanelTests : BunitTestContextBase
{
    [Fact]
    public void FilterToggle_OpensPanelWithGroupEditor()
    {
        AuthorizeAdmin();
        var provider = ProviderReturning(new TestProduct { Id = Guid.NewGuid(), Name = "Alpha", IsActive = true });
        Services.AddSingleton(provider);

        var cut = RenderListWithColumn(provider, p => p.Name);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-filter-toggle")));

        cut.Find(".base-list-filter-toggle").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".filter-panel")));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".filter-group")));
    }

    /// <summary>
    /// The panel covers the list behind an overlay that swallows every click outside it, so a keyboard
    /// user who opened it had no way back out.
    /// </summary>
    [Fact]
    public void EscapeClosesTheFilterPanel()
    {
        AuthorizeAdmin();
        var provider = ProviderReturning(new TestProduct { Id = Guid.NewGuid(), Name = "Alpha", IsActive = true });
        Services.AddSingleton(provider);

        var cut = RenderListWithColumn(provider, p => p.Name);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-filter-toggle")));
        cut.Find(".base-list-filter-toggle").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".filter-panel")));

        cut.Find(".filter-panel").KeyDown(Key.Escape);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".filter-panel")));
        cut.WaitForAssertion(() => Assert.Contains(JSInterop.Invocations, i => i.Identifier == "restoreFocus"));
    }

    /// <summary>
    /// It announces itself as a modal, so the keyboard has to follow it instead of staying on the list
    /// underneath.
    /// </summary>
    [Fact]
    public void FilterPanel_IsAModalTheKeyboardCanReach()
    {
        AuthorizeAdmin();
        var provider = ProviderReturning(new TestProduct { Id = Guid.NewGuid(), Name = "Alpha", IsActive = true });
        Services.AddSingleton(provider);

        var cut = RenderListWithColumn(provider, p => p.Name);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-filter-toggle")));
        cut.Find(".base-list-filter-toggle").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".filter-panel")));
        var panel = cut.Find(".filter-panel");

        Assert.Equal("dialog", panel.GetAttribute("role"));
        Assert.Equal("true", panel.GetAttribute("aria-modal"));
        Assert.Equal("-1", panel.GetAttribute("tabindex"));
    }

    [Fact]
    public void DisabledFiltering_HidesFilterToggle()
    {
        AuthorizeAdmin();
        var provider = ProviderReturning(new TestProduct { Id = Guid.NewGuid(), Name = "Alpha" });
        Services.AddSingleton(provider);

        var configuration = new BaseListBuilder<TestProduct>()
            .Column(p => p.Name)
            .Filtering(f => f.Disable())
            .Build();

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.Configuration, configuration));

        cut.WaitForState(() => cut.FindAll(".base-list-grid").Count > 0);

        Assert.Empty(cut.FindAll(".base-list-filter-toggle"));
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

    private IRenderedComponent<BaseList<TestProduct>> RenderListWithColumn(
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
