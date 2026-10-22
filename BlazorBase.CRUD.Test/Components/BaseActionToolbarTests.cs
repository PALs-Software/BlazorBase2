using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The toolbar enables an item-bound action by its scope and the rows' enabled predicate, tells the user why a button
/// is disabled, and reports the rows that block it while the pointer or the keyboard is on that button. The
/// multi-action menu button needs a Fluent menu provider these tests do not have, so only single-action groups are
/// rendered here.
/// </summary>
public class BaseActionToolbarTests : BunitTestContextBase
{
    public BaseActionToolbarTests()
    {
        Services.AddLocalization();
        this.AddAuthorization().SetAuthorized("admin");
    }

    private IStringLocalizer Framework => Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));

    private static TestProduct Product(string name, bool active = true) => new() { Id = Guid.NewGuid(), Name = name, IsActive = active };

    private static CrudActionGroup<TestProduct> Group(CrudAction<TestProduct> action) => new()
    {
        Name = "group",
        Contexts = CrudActionContext.ListToolbar,
        Actions = [action]
    };

    private IRenderedComponent<BaseActionToolbar<TestProduct>> RenderToolbar(
        CrudAction<TestProduct> action,
        IReadOnlyCollection<TestProduct>? selection = null,
        Action<IReadOnlyCollection<TestProduct>>? onBlocked = null,
        CrudActionGroup<TestProduct>? group = null)
    {
        return Render<BaseActionToolbar<TestProduct>>(parameters => parameters
            .Add(p => p.ActionGroups, [group ?? Group(action)])
            .Add(p => p.Context, CrudActionContext.ListToolbar)
            .Add(p => p.SelectedItems, selection ?? [])
            .Add(p => p.OnBlockedItemsChanged, items => onBlocked?.Invoke(items)));
    }

    private static bool IsDisabled(IRenderedComponent<BaseActionToolbar<TestProduct>> cut) =>
        cut.Find("fluent-button").HasAttribute("disabled");

    private static CrudAction<TestProduct> Action(CrudActionScope scope, Func<TestProduct, bool>? enabled = null) => new()
    {
        Caption = "Run",
        Scope = scope,
        Enabled = enabled,
        Action = _ => Task.CompletedTask
    };

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void SingleAction_IsEnabledOnlyWithExactlyOneSelectedRow(int rows, bool disabled)
    {
        var selection = Enumerable.Range(0, rows).Select(i => Product($"P{i}")).ToList();

        var cut = RenderToolbar(Action(CrudActionScope.Single), selection);

        Assert.Equal(disabled, IsDisabled(cut));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(4, false)]
    public void MultipleAction_IsEnabledWithAnySelectedRow(int rows, bool disabled)
    {
        var selection = Enumerable.Range(0, rows).Select(i => Product($"P{i}")).ToList();

        var cut = RenderToolbar(Action(CrudActionScope.Multiple), selection);

        Assert.Equal(disabled, IsDisabled(cut));
    }

    [Fact]
    public void ActionWithoutScope_IsAlwaysEnabled()
    {
        var cut = RenderToolbar(Action(CrudActionScope.None));

        Assert.False(IsDisabled(cut));
        Assert.Empty(cut.FindAll(".base-action-toolbar-reason"));
    }

    [Fact]
    public void IsBulkAction_StillDisablesTheButtonWithoutASelection()
    {
        var action = new CrudAction<TestProduct> { Caption = "Bulk", IsBulkAction = true, Action = _ => Task.CompletedTask };

        Assert.True(IsDisabled(RenderToolbar(action)));
        Assert.False(IsDisabled(RenderToolbar(action, [Product("A")])));
    }

    [Fact]
    public void DisabledButton_HasTheReasonAsTooltipAndDescription()
    {
        var cut = RenderToolbar(Action(CrudActionScope.Single));

        var wrapper = cut.Find(".base-action-toolbar-item");
        var reason = cut.Find(".base-action-toolbar-reason");

        Assert.Equal(Framework["ActionSelectRowFirst"].Value, wrapper.GetAttribute("title"));
        Assert.Equal(Framework["ActionSelectRowFirst"].Value, reason.TextContent);
        Assert.Equal(reason.Id, wrapper.GetAttribute("aria-describedby"));
        Assert.Equal(reason.Id, cut.Find("fluent-button").GetAttribute("aria-describedby"));
        Assert.Equal("0", wrapper.GetAttribute("tabindex"));
    }

    [Fact]
    public void SingleAction_WithTwoRows_AsksForExactlyOne()
    {
        var cut = RenderToolbar(Action(CrudActionScope.Single), [Product("A"), Product("B")]);

        Assert.Equal(Framework["ActionSelectExactlyOne"].Value, cut.Find(".base-action-toolbar-item").GetAttribute("title"));
    }

    [Fact]
    public void MultipleAction_WithoutRows_AsksForRows()
    {
        var cut = RenderToolbar(Action(CrudActionScope.Multiple));

        Assert.Equal(Framework["ActionSelectRowsFirst"].Value, cut.Find(".base-action-toolbar-item").GetAttribute("title"));
    }

    [Fact]
    public void BlockingRows_DisableTheActionAndTheTooltipNamesCountAndReason()
    {
        var action = Action(CrudActionScope.Multiple, p => p.IsActive);
        action.DisabledReason = "Product is inactive";
        var selection = new List<TestProduct> { Product("A"), Product("B", false), Product("C", false), Product("D"), Product("E") };

        var cut = RenderToolbar(action, selection);

        Assert.True(IsDisabled(cut));
        var expected = string.Format(Framework["ActionNotPossibleForRows"].Value, 2, 5) + ": Product is inactive";
        Assert.Equal(expected, cut.Find(".base-action-toolbar-item").GetAttribute("title"));
        Assert.Equal(expected, cut.Find(".base-action-toolbar-reason").TextContent);
    }

    [Fact]
    public void HoveringADisabledButton_RaisesTheBlockingRowsAndLeavingClearsThem()
    {
        var bad = Product("Bad", false);
        var raised = new List<IReadOnlyCollection<TestProduct>>();
        var cut = RenderToolbar(Action(CrudActionScope.Multiple, p => p.IsActive), [Product("A"), bad], raised.Add);
        var wrapper = cut.Find(".base-action-toolbar-item");

        wrapper.MouseEnter();
        Assert.Same(bad, Assert.Single(Assert.Single(raised)));

        wrapper.MouseLeave();
        Assert.Equal(2, raised.Count);
        Assert.Empty(raised[1]);
    }

    [Fact]
    public void FocusingADisabledButton_RaisesTheBlockingRowsAndBlurClearsThem()
    {
        var bad = Product("Bad", false);
        var raised = new List<IReadOnlyCollection<TestProduct>>();
        var cut = RenderToolbar(Action(CrudActionScope.Multiple, p => p.IsActive), [bad], raised.Add);
        var wrapper = cut.Find(".base-action-toolbar-item");

        wrapper.FocusIn();
        Assert.Same(bad, Assert.Single(Assert.Single(raised)));

        wrapper.FocusOut();
        Assert.Empty(raised[1]);
    }

    [Fact]
    public void HoveringAButtonWithoutBlockingRows_RaisesNothing()
    {
        var raised = new List<IReadOnlyCollection<TestProduct>>();
        var cut = RenderToolbar(Action(CrudActionScope.Single), [], raised.Add);

        cut.Find(".base-action-toolbar-item").MouseEnter();
        cut.Find(".base-action-toolbar-item").MouseLeave();

        Assert.Empty(raised);
    }

    [Fact]
    public void BlockedRows_AreClearedWhenTheSelectionChangesUnderTheHoveredButton()
    {
        var bad = Product("Bad", false);
        var raised = new List<IReadOnlyCollection<TestProduct>>();
        var cut = RenderToolbar(Action(CrudActionScope.Multiple, p => p.IsActive), [bad], raised.Add);
        cut.Find(".base-action-toolbar-item").MouseEnter();

        cut.Render(parameters => parameters.Add(p => p.SelectedItems, [Product("Good")]));

        Assert.Empty(raised.Last());
    }

    [Fact]
    public void EnabledButton_RunsTheActionWithTheSelectedRows()
    {
        var a = Product("A");
        var b = Product("B");
        CrudActionEventArgs<TestProduct>? received = null;
        var action = Action(CrudActionScope.Multiple);
        action.Action = args =>
        {
            received = args;
            return Task.CompletedTask;
        };

        var cut = RenderToolbar(action, [a, b]);
        cut.Find("fluent-button").Click();

        Assert.NotNull(received);
        Assert.Equal([a, b], received.SelectedItems);
        Assert.Equal(CrudActionContext.ListToolbar, received.Context);
    }

    [Fact]
    public void SingleAction_ReceivesTheSelectedRowAsItem()
    {
        var a = Product("A");
        CrudActionEventArgs<TestProduct>? received = null;
        var action = Action(CrudActionScope.Single);
        action.Action = args =>
        {
            received = args;
            return Task.CompletedTask;
        };

        var cut = RenderToolbar(action, [a]);
        cut.Find("fluent-button").Click();

        Assert.NotNull(received);
        Assert.Same(a, received.Item);
        Assert.Equal([a], received.SelectedItems);
    }

    [Fact]
    public void VisibleFalse_HidesTheActionAndItsGroup()
    {
        var action = Action(CrudActionScope.None);
        action.Visible = _ => Task.FromResult(false);

        var cut = RenderToolbar(action);

        Assert.Empty(cut.FindAll(".base-action-toolbar-item"));
        Assert.Empty(cut.FindAll("fluent-button"));
    }

    [Fact]
    public void VisibleFalseOnTheGroup_HidesTheAction()
    {
        var action = Action(CrudActionScope.None);
        var group = Group(action);
        group.Visible = _ => Task.FromResult(false);

        var cut = RenderToolbar(action, group: group);

        Assert.Empty(cut.FindAll("fluent-button"));
    }

    [Fact]
    public void Visible_IsEvaluatedWithTheSelectedRow()
    {
        var action = Action(CrudActionScope.Single);
        action.Visible = args => Task.FromResult(args.Item is { IsActive: true });

        Assert.Single(RenderToolbar(action, [Product("A")]).FindAll("fluent-button"));
        Assert.Empty(RenderToolbar(action, [Product("B", false)]).FindAll("fluent-button"));
    }

    [Fact]
    public void ItemBoundAction_VisibleIsAskedWithoutARowWhenNothingIsSelected()
    {
        var action = Action(CrudActionScope.Single);
        action.Visible = args => Task.FromResult(args.Item is null || args.Item.IsActive);

        var cut = RenderToolbar(action);

        Assert.Single(cut.FindAll("fluent-button"));
        Assert.True(IsDisabled(cut));

        var hidden = Action(CrudActionScope.Single);
        hidden.Visible = _ => Task.FromResult(false);

        Assert.Empty(RenderToolbar(hidden).FindAll("fluent-button"));
    }

    [Fact]
    public void Visible_IsNotEvaluatedAgainWhenOnlyUnrelatedParametersChange()
    {
        var calls = 0;
        var action = Action(CrudActionScope.None);
        action.Visible = _ =>
        {
            calls++;
            return Task.FromResult(true);
        };
        var cut = RenderToolbar(action);
        var afterFirst = calls;

        cut.Render(parameters => parameters.Add(p => p.OnActionExecuted, () => { }));

        Assert.Equal(afterFirst, calls);
    }

    [Fact]
    public void ExplicitContexts_WinOverTheScopeDefault()
    {
        var action = Action(CrudActionScope.Single);
        action.Contexts = CrudActionContext.ContextMenu;

        var cut = RenderToolbar(action, [Product("A")]);

        Assert.Empty(cut.FindAll("fluent-button"));
    }
}
