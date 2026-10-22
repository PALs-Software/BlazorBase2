using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// Selecting rows is only offered where something consumes a selection (a delete or a bulk action), and a list
/// whose rows have nothing to offer gets no more-button column and no menu at all.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListSelectionAvailabilityTests : BunitTestContextBase
{
    private const string InteropModulePath = "./_content/BlazorBase.CRUD/js/baseListInterop.js";

    public BaseListSelectionAvailabilityTests() => Services.AddLocalization();

    private string L(string key) => Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources))[key].Value;

    [Fact]
    public void ReadOnlyListWithoutActions_HasNeitherMoreButtonNorContextMenu()
    {
        var cut = RenderList(allowEdit: false, allowDelete: false);

        Assert.Empty(cut.FindAll(".base-list-row-menu"));
        Assert.Empty(cut.FindAll(".base-list-row-menu-column"));

        cut.Find(".base-list-grid").ContextMenu(new MouseEventArgs { ClientX = 40, ClientY = 40 });

        Assert.Empty(cut.FindAll(".context-menu"));
    }

    [Fact]
    public void ReadOnlyListWithRowActions_OffersTheActionButNoSelection()
    {
        var cut = RenderList(allowEdit: false, allowDelete: false, contextMenuActions: _ => builder => builder.AddContent(0, "custom-action"));

        OpenRowMenu(cut);

        cut.WaitForAssertion(() => Assert.Contains("custom-action", cut.Find(".context-menu").TextContent));
        Assert.Empty(cut.FindAll(".context-menu [role=\"menuitem\"]"));
        Assert.Empty(cut.FindAll(".context-menu .context-menu-divider"));
    }

    [Fact]
    public void DeletableList_OffersDeleteSelectMoreSelectAllAndDeselectAll()
    {
        var cut = RenderList(allowEdit: false, allowDelete: true);

        OpenRowMenu(cut);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        Assert.Equal(
            [L("Delete"), L("SelectMore"), L("SelectAll"), L("DeselectAll")],
            cut.FindAll(".context-menu [role=\"menuitem\"]").Select(item => item.TextContent.Trim()));
    }

    [Fact]
    public void ReadOnlyListWithBulkAction_OffersSelection()
    {
        var group = new CrudActionGroup<TestProduct>
        {
            Name = "bulk",
            Contexts = CrudActionContext.ListToolbar,
            Actions = [new CrudAction<TestProduct> { Caption = "Bulk", IsBulkAction = true, Action = _ => Task.CompletedTask }],
        };

        var cut = RenderList(allowEdit: false, allowDelete: false, actionGroups: [group]);

        OpenRowMenu(cut);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        Assert.Equal(
            [L("SelectMore"), L("SelectAll"), L("DeselectAll")],
            cut.FindAll(".context-menu [role=\"menuitem\"]").Select(item => item.TextContent.Trim()));
    }

    private static void OpenRowMenu(IRenderedComponent<BaseList<TestProduct>> cut)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-row-menu")));
        cut.Find(".base-list-row-menu").Click();
    }

    private IRenderedComponent<BaseList<TestProduct>> RenderList(
        bool allowEdit,
        bool allowDelete,
        RenderFragment<TestProduct>? contextMenuActions = null,
        List<CrudActionGroup<TestProduct>>? actionGroups = null)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        var module = JSInterop.SetupModule(InteropModulePath);
        module.Setup<bool>("isPointOnDataRow", _ => true).SetResult(true);
        module.SetupVoid("rememberFocus", _ => true).SetVoidResult();
        module.SetupVoid("restoreFocus", _ => true).SetVoidResult();

        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct>
            {
                Items = [new TestProduct { Id = Guid.NewGuid(), Name = "Alpha", IsActive = true }],
                TotalCount = 1,
            });
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.AllowEdit, allowEdit)
            .Add(p => p.AllowDelete, allowDelete)
            .Add(p => p.ContextMenuActions, contextMenuActions)
            .Add(p => p.ActionGroups, actionGroups)
            .Add(p => p.ChildContent, BuildColumn(p => p.Name)));

        cut.WaitForState(() => cut.FindAll(".base-list-grid").Count > 0);

        return cut;
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
