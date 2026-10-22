using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The tile layout of a phone: a tap opens a tile, a long press (or "Select more") picks tiles one by one, the
/// three-dot button of every tile offers the row menu, and a bar at the bottom shows the selection with the item-bound
/// actions.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListPhoneTilesTests : BunitTestContextBase
{
    private const string MobileModulePath = "./_content/BlazorBase.CRUD/js/baseListMobile.js";

    private readonly List<BaseDialogData<TestProduct>> openedCards = [];
    private readonly List<TestProduct> openedEvents = [];
    private readonly List<TestProduct> selectedEvents = [];

    public BaseListPhoneTilesTests() => Services.AddLocalization();

    private IStringLocalizer Framework => Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));

    [Fact]
    public void Tap_OpensTheEditCard_AndRaisesOnItemOpened()
    {
        var cut = RenderList();

        cut.FindAll(".base-list-card-body")[1].Click();

        var card = Assert.Single(openedCards);
        Assert.False(card.ReadOnly);
        Assert.Equal("Item 1", card.Model.Name);
        Assert.Equal(["Item 1"], openedEvents.Select(item => item.Name));
        Assert.Equal(["Item 1"], selectedEvents.Select(item => item.Name));
    }

    [Fact]
    public void Tap_OnAListTheUserMayOnlyRead_OpensTheReadOnlyCard()
    {
        var cut = RenderList(allowEdit: false);

        cut.FindAll(".base-list-card-body")[0].Click();

        var card = Assert.Single(openedCards);
        Assert.True(card.ReadOnly);
        Assert.Equal("Item 0", card.Model.Name);
        Assert.Single(openedEvents);
    }

    [Fact]
    public void Tap_OnAListWithoutACardDefinition_OpensNothing()
    {
        var cut = RenderList(allowEdit: false, withCard: false);

        cut.FindAll(".base-list-card-body")[0].Click();

        Assert.Empty(openedCards);
        Assert.Empty(openedEvents);
    }

    [Fact]
    public void EveryTile_CarriesItsPosition_SoALongPressCanNameIt()
    {
        var cut = RenderList();

        Assert.Equal(["0", "1", "2"], cut.FindAll(".base-list-card").Select(tile => tile.GetAttribute("data-tile-index")));
    }

    [Fact]
    public async Task LongPress_StartsMultiSelectMode_WithThatTileSelected()
    {
        var cut = RenderList(allowDelete: true);
        Assert.Empty(cut.FindAll(".base-list-card-check"));
        Assert.Empty(cut.FindAll(".base-list-selection-bar"));

        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(1));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".base-list-card-check").Count));
        var selected = Assert.Single(cut.FindAll(".base-list-card.is-selected"));
        Assert.Equal("1", selected.GetAttribute("data-tile-index"));
        Assert.NotNull(cut.Find(".base-list-selection-bar"));
        Assert.Empty(openedCards);
    }

    [Fact]
    public async Task LongPress_WhereNothingCanBeDoneWithASelection_DoesNothing()
    {
        var cut = RenderList(allowDelete: false);

        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(1));

        Assert.Empty(cut.FindAll(".base-list-card-check"));
        Assert.Empty(cut.FindAll(".base-list-card.is-selected"));
        Assert.Empty(cut.FindAll(".base-list-selection-bar"));
    }

    [Fact]
    public async Task InMultiSelectMode_ATapTogglesTheTile_AndDoesNotOpenIt()
    {
        var cut = RenderList(allowDelete: true);
        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(0));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".base-list-card-check").Count));

        cut.FindAll(".base-list-card-body")[2].Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card.is-selected").Count));
        Assert.Empty(openedCards);
        Assert.Empty(openedEvents);
        Assert.Empty(selectedEvents);

        cut.FindAll(".base-list-card-body")[2].Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".base-list-card.is-selected")));
    }

    [Fact]
    public async Task InMultiSelectMode_TakingOutTheLastTile_LeavesTheMode()
    {
        var cut = RenderList(allowDelete: true);
        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(0));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-selection-bar")));

        cut.FindAll(".base-list-card-body")[0].Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-list-selection-bar")));
        Assert.Empty(cut.FindAll(".base-list-card-check"));
    }

    [Fact]
    public void SelectMoreInTheTileMenu_StartsMultiSelectMode()
    {
        var cut = RenderList(allowDelete: true);

        OpenTileMenu(cut, 2);
        MenuItem(cut, Framework["SelectMore"].Value).Click();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".base-list-card-check").Count));
        Assert.Equal("2", Assert.Single(cut.FindAll(".base-list-card.is-selected")).GetAttribute("data-tile-index"));
        Assert.NotNull(cut.Find(".base-list-selection-bar"));
    }

    [Fact]
    public void SelectAllInTheTileMenu_StartsMultiSelectModeWithEveryTileSelected()
    {
        var cut = RenderList(allowDelete: true);

        OpenTileMenu(cut, 0);
        MenuItem(cut, Framework["SelectAll"].Value).Click();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".base-list-card.is-selected").Count));
        Assert.Contains(string.Format(Framework["SelectedCount"].Value, 3), cut.Find(".base-list-selection-count").TextContent);
    }

    [Fact]
    public async Task SelectionBar_ShowsTheCount_AClearButton_AndDelete()
    {
        var cut = RenderList(allowDelete: true);
        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(0));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-selection-bar")));
        cut.FindAll(".base-list-card-body")[1].Click();

        cut.WaitForAssertion(() => Assert.Contains(string.Format(Framework["SelectedCount"].Value, 2), cut.Find(".base-list-selection-count").TextContent));
        Assert.Equal(Framework["DeselectAll"].Value, cut.Find(".base-list-selection-close").GetAttribute("aria-label"));
        Assert.Equal(string.Format(Framework["DeleteCount"].Value, 2), cut.Find(".base-list-selection-delete").GetAttribute("aria-label"));

        cut.Find(".base-list-selection-close").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-list-selection-bar")));
        Assert.Empty(cut.FindAll(".base-list-card.is-selected"));
        Assert.Empty(cut.FindAll(".base-list-card-check"));
    }

    [Fact]
    public async Task SelectionBar_HasNoDeleteButton_WhenTheUserMayNotDelete()
    {
        var selectable = Action("Run", CrudActionScope.Multiple);
        var cut = RenderList(allowDelete: false, groups: [Group(selectable)]);

        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(0));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-selection-bar")));
        Assert.Empty(cut.FindAll(".base-list-selection-delete"));
    }

    [Fact]
    public async Task SelectionBar_OffersTheItemBoundActions_AndTheHeaderKeepsTheOthers()
    {
        var cut = RenderList(groups: [Group(Action("Run", CrudActionScope.Multiple)), Group(Action("Export", CrudActionScope.None))]);

        Assert.DoesNotContain("Run", cut.Find(".base-list-header").TextContent);
        Assert.Contains("Export", cut.Find(".base-list-header").TextContent);

        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(0));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-selection-bar")));
        var bar = cut.Find(".base-list-selection-bar");
        Assert.Contains("Run", bar.TextContent);
        Assert.DoesNotContain("Export", bar.TextContent);
        Assert.DoesNotContain("Run", cut.Find(".base-list-header").TextContent);
    }

    [Fact]
    public async Task SelectionBar_RunsAnActionOnTheSelectedTiles()
    {
        CrudActionEventArgs<TestProduct>? received = null;
        var cut = RenderList(groups: [Group(Action("Run", CrudActionScope.Multiple, args => received = args))]);
        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(0));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-selection-bar")));
        cut.FindAll(".base-list-card-body")[2].Click();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card.is-selected").Count));

        cut.Find(".base-list-selection-bar .base-action-toolbar-inline fluent-button").Click();

        Assert.NotNull(received);
        Assert.Equal(["Item 0", "Item 2"], received.SelectedItems.Select(item => item.Name).Order());
        Assert.Equal(CrudActionContext.ListToolbar, received.Context);
    }

    [Fact]
    public async Task SelectionBar_DisablesASingleRowAction_WhileSeveralTilesAreSelected_AndSaysWhy()
    {
        var cut = RenderList(groups: [Group(Action("Run now", CrudActionScope.Single))]);
        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(0));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-selection-bar")));

        Assert.False(IsDisabled(cut.Find(".base-list-selection-bar"), "Run now"));

        cut.FindAll(".base-list-card-body")[1].Click();

        cut.WaitForAssertion(() => Assert.True(IsDisabled(cut.Find(".base-list-selection-bar"), "Run now")));
        Assert.Contains(Framework["ActionSelectExactlyOne"].Value, cut.Find(".base-list-selection-bar .base-action-toolbar-reason").TextContent);

        cut.Find(".base-list-selection-bar .base-action-toolbar-item-disabled").Click();

        cut.WaitForAssertion(() => Assert.Equal(Framework["ActionSelectExactlyOne"].Value, cut.Find(".base-list-selection-hint").TextContent.Trim()));
    }

    [Fact]
    public async Task SelectionBar_DisablesAnActionWhenARowFailsItsPredicate_WithTheReason()
    {
        var action = Action("Run", CrudActionScope.Multiple);
        action.Enabled = product => product.Name != "Item 1";
        action.DisabledReason = "Item 1 cannot run";
        var cut = RenderList(groups: [Group(action)]);
        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(0));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-selection-bar")));
        Assert.False(IsDisabled(cut.Find(".base-list-selection-bar"), "Run"));

        cut.FindAll(".base-list-card-body")[1].Click();

        cut.WaitForAssertion(() => Assert.True(IsDisabled(cut.Find(".base-list-selection-bar"), "Run")));
        Assert.Contains("Item 1 cannot run", cut.Find(".base-list-selection-bar .base-action-toolbar-reason").TextContent);
    }

    [Fact]
    public void TileMenu_OffersViewOrEdit_Delete_AndTheDeclaredActions()
    {
        var cut = RenderList(allowDelete: true, groups: [Group(Action("Run now", CrudActionScope.Single))]);

        OpenTileMenu(cut, 1);

        var entries = cut.FindAll(".context-menu [role=\"menuitem\"]").Select(item => item.TextContent.Trim()).ToList();
        Assert.Equal(
            [Framework["Edit"].Value, Framework["Delete"].Value, "Run now", Framework["SelectMore"].Value, Framework["SelectAll"].Value],
            entries);
    }

    [Fact]
    public void TileMenu_OfAListTheUserMayOnlyRead_OffersView()
    {
        var cut = RenderList(allowEdit: false);

        OpenTileMenu(cut, 0);

        Assert.Equal(Framework["View"].Value, cut.FindAll(".context-menu [role=\"menuitem\"]")[0].TextContent.Trim());
    }

    [Fact]
    public void TileMenu_ShowsADisabledActionWithItsReasonAsText()
    {
        var action = Action("Run now", CrudActionScope.Single);
        action.Enabled = product => product.Name != "Item 1";
        action.DisabledReason = "Item 1 cannot run";
        var cut = RenderList(groups: [Group(action)]);

        OpenTileMenu(cut, 1);

        var entry = cut.FindAll(".context-menu [role=\"menuitem\"]").First(item => item.TextContent.Contains("Run now"));
        Assert.Equal("true", entry.GetAttribute("aria-disabled"));
        Assert.Contains("Item 1 cannot run", entry.QuerySelector(".context-menu-reason")!.TextContent);
    }

    [Fact]
    public void TileMenu_RunsTheDeclaredActionOnTheTileItWasOpenedOn()
    {
        CrudActionEventArgs<TestProduct>? received = null;
        var cut = RenderList(groups: [Group(Action("Run now", CrudActionScope.Single, args => received = args))]);

        OpenTileMenu(cut, 2);
        MenuItem(cut, "Run now").Click();

        Assert.Equal("Item 2", received?.Item?.Name);
        Assert.Equal(CrudActionContext.ContextMenu, received?.Context);
    }

    [Fact]
    public void EveryTile_HasAThreeDotButton_ThatTheMetricsHaveRoomFor()
    {
        var cut = RenderList();

        Assert.Equal(3, cut.FindAll(".base-list-card-more").Count);
        Assert.True(BaseListTileMetrics.Resolve(new MobileCardPlan<TestProduct> { Title = TitleColumn() }, hasActions: false) * BaseListTileMetrics.RootFontSizePixels >= 44 + 2);
    }

    [Fact]
    public async Task TheTileHeight_IsTheSame_WhateverTheSelectionState()
    {
        var cut = RenderList(allowDelete: true);
        var before = cut.Find(".base-list-cards").GetAttribute("style");

        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(0));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-card-check")));

        Assert.Equal(before, cut.Find(".base-list-cards").GetAttribute("style"));
    }

    [Fact]
    public async Task TheBackButton_LeavesMultiSelectModeInsteadOfThePage()
    {
        var cut = RenderList(allowDelete: true);
        var navigation = Services.GetRequiredService<NavigationManager>();
        var before = navigation.Uri;
        await cut.InvokeAsync(() => cut.Instance.OnTileLongPressAsync(0));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-selection-bar")));
        cut.Render();

        navigation.NavigateTo("/somewhere-else");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-list-selection-bar")));
        Assert.Equal(before, navigation.Uri);
    }

    [Fact]
    public void LongPressScript_IsAttachedToTheTileList()
    {
        var module = JSInterop.SetupModule(MobileModulePath);
        module.SetupVoid("observeTiles", _ => true).SetVoidResult();

        RenderList();

        Assert.Single(module.Invocations["observeTiles"]);
    }

    private static bool IsDisabled(AngleSharp.Dom.IElement bar, string caption) =>
        bar.QuerySelectorAll(".base-action-toolbar-item")
            .First(item => item.TextContent.Contains(caption))
            .ClassList.Contains("base-action-toolbar-item-disabled");

    private static AngleSharp.Dom.IElement MenuItem(IRenderedComponent<BaseList<TestProduct>> cut, string text) =>
        cut.FindAll(".context-menu [role=\"menuitem\"]").First(item => item.TextContent.Trim() == text);

    private static void OpenTileMenu(IRenderedComponent<BaseList<TestProduct>> cut, int tile)
    {
        cut.FindAll(".base-list-card-more")[tile].Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu:not(.context-menu-measuring)")));
    }

    private static CrudAction<TestProduct> Action(string caption, CrudActionScope scope, Action<CrudActionEventArgs<TestProduct>>? onExecute = null) => new()
    {
        Caption = caption,
        Scope = scope,
        Action = args =>
        {
            onExecute?.Invoke(args);
            return Task.CompletedTask;
        }
    };

    private static CrudActionGroup<TestProduct> Group(params CrudAction<TestProduct>[] actions) => new()
    {
        Name = "group",
        Actions = [.. actions]
    };

    private static PropertyColumnConfig<TestProduct> TitleColumn() => new() { PropertyName = "Name", Property = product => product.Name };

    private IRenderedComponent<BaseList<TestProduct>> RenderList(
        bool allowEdit = true,
        bool allowDelete = false,
        bool withCard = true,
        CrudActionGroup<TestProduct>[]? groups = null)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        var dialogService = Substitute.For<IDialogService>();
        var reference = Substitute.For<IDialogReference>();
        reference.Result.Returns(Task.FromResult(DialogResult.Cancel()));
        dialogService.ShowDialogAsync<BaseDialog<TestProduct>>(Arg.Any<object>(), Arg.Any<DialogParameters>())
            .Returns(call =>
            {
                openedCards.Add((BaseDialogData<TestProduct>)call[0]);
                return Task.FromResult(reference);
            });
        Services.AddSingleton(dialogService);

        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<BaseQuery>();
                var items = Enumerable.Range(query.Skip, Math.Max(0, Math.Min(query.Take, 3 - query.Skip)))
                    .Select(index => new TestProduct { Id = new Guid(index + 1, 0, 0, new byte[8]), Name = $"Item {index}", Stock = index })
                    .ToList();

                return new BaseQueryResult<TestProduct> { Items = items, TotalCount = 3 };
            });
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.AllowEdit, allowEdit)
            .Add(p => p.AllowDelete, allowDelete)
            .Add(p => p.MobileLayout, BaseListMobileLayout.Always)
            .Add(p => p.EnableDeepLinking, false)
            .Add(p => p.ActionGroups, [.. groups ?? []])
            .Add(p => p.CardConfiguration, withCard ? new BaseCardConfiguration<TestProduct>() : null)
            .Add(p => p.OnItemSelected, EventCallback.Factory.Create<TestProduct>(this, selectedEvents.Add))
            .Add(p => p.OnItemOpened, EventCallback.Factory.Create<TestProduct>(this, openedEvents.Add))
            .Add(p => p.ChildContent, BuildColumns()));

        cut.WaitForState(() => cut.FindAll(".base-list-card").Count == 3);

        return cut;
    }

    private static RenderFragment BuildColumns() => builder =>
    {
        builder.OpenComponent<BaseColumn<TestProduct>>(0);
        builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), (Expression<Func<TestProduct, object?>>)(p => p.Name));
        builder.CloseComponent();

        builder.OpenComponent<BaseColumn<TestProduct>>(3);
        builder.AddComponentParameter(4, nameof(BaseColumn<TestProduct>.Property), (Expression<Func<TestProduct, object?>>)(p => p.Stock));
        builder.CloseComponent();
    };
}
