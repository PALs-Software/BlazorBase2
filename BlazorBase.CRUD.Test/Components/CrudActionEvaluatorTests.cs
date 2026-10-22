using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using System.Security.Claims;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The rules of the action model: which rows an action works on, when it is enabled, why it is not and when it is
/// visible. They live in <see cref="CrudActionEvaluator{TModel}"/>, free of rendering, so the toolbar and the row menu
/// cannot disagree.
/// </summary>
public class CrudActionEvaluatorTests
{
    private static TestProduct Product(string name, bool active = true) => new() { Id = Guid.NewGuid(), Name = name, IsActive = active };

    private static CrudAction<TestProduct> Action(CrudActionScope scope, Func<TestProduct, bool>? enabled = null) => new()
    {
        Caption = "Act",
        Scope = scope,
        Enabled = enabled,
        Action = _ => Task.CompletedTask
    };

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Single_IsEnabledOnlyWithExactlyOneRow(int rows, bool expected)
    {
        var selection = Enumerable.Range(0, rows).Select(i => Product($"P{i}")).ToList();

        var state = CrudActionEvaluator<TestProduct>.Evaluate(Action(CrudActionScope.Single), selection);

        Assert.Equal(expected, state.IsEnabled);
    }

    [Fact]
    public void Single_ExplainsWhyItIsDisabled()
    {
        var action = Action(CrudActionScope.Single);

        Assert.Equal(CrudActionBlock.NoSelection, CrudActionEvaluator<TestProduct>.Evaluate(action, []).Block);
        Assert.Equal(CrudActionBlock.NeedsExactlyOne, CrudActionEvaluator<TestProduct>.Evaluate(action, [Product("A"), Product("B")]).Block);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public void Multiple_IsEnabledWithAtLeastOneRow(int rows, bool expected)
    {
        var selection = Enumerable.Range(0, rows).Select(i => Product($"P{i}")).ToList();

        var state = CrudActionEvaluator<TestProduct>.Evaluate(Action(CrudActionScope.Multiple), selection);

        Assert.Equal(expected, state.IsEnabled);
    }

    [Fact]
    public void None_IsAlwaysEnabled()
    {
        var action = Action(CrudActionScope.None);

        Assert.True(CrudActionEvaluator<TestProduct>.Evaluate(action, []).IsEnabled);
        Assert.True(CrudActionEvaluator<TestProduct>.Evaluate(action, [Product("A"), Product("B")]).IsEnabled);
    }

    [Fact]
    public void Multiple_IsDisabledWhenOneRowFailsTheEnabledPredicate()
    {
        var good = Product("Good");
        var bad = Product("Bad", active: false);
        var action = Action(CrudActionScope.Multiple, p => p.IsActive);

        var state = CrudActionEvaluator<TestProduct>.Evaluate(action, [good, bad, Product("Other")]);

        Assert.False(state.IsEnabled);
        Assert.Equal(CrudActionBlock.ItemsBlocked, state.Block);
        Assert.Same(bad, Assert.Single(state.BlockingItems));
        Assert.Equal(3, state.Targets.Count);
    }

    [Fact]
    public void Single_IsDisabledWhenItsRowFailsTheEnabledPredicate()
    {
        var bad = Product("Bad", active: false);

        var state = CrudActionEvaluator<TestProduct>.Evaluate(Action(CrudActionScope.Single, p => p.IsActive), [bad]);

        Assert.False(state.IsEnabled);
        Assert.Same(bad, Assert.Single(state.BlockingItems));
    }

    [Fact]
    public void BlockingRows_GiveTheirReasonFromTheProviderOrTheStaticText()
    {
        var action = Action(CrudActionScope.Multiple, p => p.IsActive);
        action.DisabledReason = "Product is inactive";
        action.DisabledReasonProvider = p => p.Name == "Special" ? "Special is locked" : null;

        var state = CrudActionEvaluator<TestProduct>.Evaluate(action, [Product("Special", false), Product("Plain", false), Product("Plain2", false)]);

        Assert.Equal(["Special is locked", "Product is inactive"], state.ItemReasons);
    }

    [Fact]
    public void DescribeBlock_NamesTheCountAndTheReason()
    {
        var action = Action(CrudActionScope.Multiple, p => p.IsActive);
        action.DisabledReason = "inactive";
        var state = CrudActionEvaluator<TestProduct>.Evaluate(action, [Product("A"), Product("B", false), Product("C", false), Product("D"), Product("E")]);

        var text = CrudActionEvaluator<TestProduct>.DescribeBlock(action, state, Framework, t => t ?? string.Empty);

        Assert.Equal("Not possible for 2 of 5 selected rows: inactive", text);
    }

    [Fact]
    public void DescribeBlock_NamesNoRowSelectionAndWrongCountSeparately()
    {
        var single = Action(CrudActionScope.Single);
        var multiple = Action(CrudActionScope.Multiple);

        Assert.Equal("Select a row first", Describe(single, []));
        Assert.Equal("Select exactly one row", Describe(single, [Product("A"), Product("B")]));
        Assert.Equal("Select one or more rows first", Describe(multiple, []));
        Assert.Null(Describe(multiple, [Product("A")]));
    }

    [Fact]
    public void DescribeBlock_UsesTheSingularForOneBlockedRow()
    {
        var action = Action(CrudActionScope.Single, p => p.IsActive);

        Assert.Equal("Not possible for the selected row", Describe(action, [Product("A", false)]));
    }

    [Fact]
    public void RowMenuTargets_TakeTheSelectionOnlyForAMultiActionOnASelectedRow()
    {
        var a = Product("A");
        var b = Product("B");
        var c = Product("C");
        IReadOnlyCollection<TestProduct> selection = [a, b];
        bool IsSelected(TestProduct p) => selection.Contains(p);

        var multiple = CrudActionEvaluator<TestProduct>.ResolveRowMenuTargets(Action(CrudActionScope.Multiple), a, selection, IsSelected);
        var multipleOutside = CrudActionEvaluator<TestProduct>.ResolveRowMenuTargets(Action(CrudActionScope.Multiple), c, selection, IsSelected);
        var single = CrudActionEvaluator<TestProduct>.ResolveRowMenuTargets(Action(CrudActionScope.Single), a, selection, IsSelected);

        Assert.Equal([a, b], multiple);
        Assert.Equal([c], multipleOutside);
        Assert.Equal([a], single);
    }

    [Fact]
    public void EventArgs_FollowTheScope()
    {
        var a = Product("A");
        var b = Product("B");
        var services = new ServiceCollectionStub();

        var single = CrudActionEvaluator<TestProduct>.BuildEventArgs(Action(CrudActionScope.Single), [a], null, services, CrudActionContext.ListToolbar);
        var multiple = CrudActionEvaluator<TestProduct>.BuildEventArgs(Action(CrudActionScope.Multiple), [a, b], null, services, CrudActionContext.ListToolbar);
        var none = CrudActionEvaluator<TestProduct>.BuildEventArgs(Action(CrudActionScope.None), [a, b], null, services, CrudActionContext.ListToolbar);

        Assert.Same(a, single.Item);
        Assert.Equal([a], single.SelectedItems);
        Assert.Null(multiple.Item);
        Assert.Equal([a, b], multiple.SelectedItems);
        Assert.Null(none.Item);
        Assert.Empty(none.SelectedItems);
        Assert.Equal(CrudActionContext.ListToolbar, single.Context);
    }

    [Fact]
    public async Task Visible_FalseHidesTheAction()
    {
        var action = Action(CrudActionScope.None);
        action.Visible = _ => Task.FromResult(false);

        Assert.False(await CrudActionEvaluator<TestProduct>.IsVisibleAsync(action, CrudActionContext.ListToolbar, null, null, []));
    }

    [Fact]
    public async Task Visible_IsAskedWithTheSingleRowAndWithoutOneWhenNoneApplies()
    {
        var asked = new List<string?>();
        var action = Action(CrudActionScope.Single);
        action.Visible = args =>
        {
            asked.Add(args.Item?.Name ?? "none");
            return Task.FromResult(args.Item?.Name == "Yes");
        };

        Assert.False(await CrudActionEvaluator<TestProduct>.IsVisibleAsync(action, CrudActionContext.ListToolbar, null, null, []));
        Assert.True(await CrudActionEvaluator<TestProduct>.IsVisibleAsync(action, CrudActionContext.ListToolbar, null, null, [Product("Yes")]));
        Assert.False(await CrudActionEvaluator<TestProduct>.IsVisibleAsync(action, CrudActionContext.ListToolbar, null, null, [Product("No")]));
        Assert.False(await CrudActionEvaluator<TestProduct>.IsVisibleAsync(action, CrudActionContext.ListToolbar, null, null, [Product("Yes"), Product("Yes")]));
        Assert.Equal(["none", "Yes", "No", "none"], asked);
    }

    [Fact]
    public async Task Visible_MultipleActionNeedsEveryRowToPass()
    {
        var action = Action(CrudActionScope.Multiple);
        action.Visible = args => Task.FromResult(args.Item!.IsActive);

        Assert.True(await CrudActionEvaluator<TestProduct>.IsVisibleAsync(action, CrudActionContext.ListToolbar, null, null, [Product("A"), Product("B")]));
        Assert.False(await CrudActionEvaluator<TestProduct>.IsVisibleAsync(action, CrudActionContext.ListToolbar, null, null, [Product("A"), Product("B", false)]));
    }

    [Fact]
    public async Task Visible_RespectsContextsRolesAndGroupPredicate()
    {
        var toolbarOnly = Action(CrudActionScope.None);
        toolbarOnly.Contexts = CrudActionContext.ListToolbar;
        var adminOnly = Action(CrudActionScope.None);
        adminOnly.VisibleForRoles = user => user.IsInRole("Admin");
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "User")], "test"));
        var group = new CrudActionGroup<TestProduct> { Name = "g", Visible = _ => Task.FromResult(false), Actions = [toolbarOnly] };

        Assert.False(await CrudActionEvaluator<TestProduct>.IsVisibleAsync(toolbarOnly, CrudActionContext.ContextMenu, null, null, []));
        Assert.False(await CrudActionEvaluator<TestProduct>.IsVisibleAsync(adminOnly, CrudActionContext.ListToolbar, user, null, []));
        Assert.False(await CrudActionEvaluator<TestProduct>.IsGroupVisibleAsync(group, CrudActionContext.ListToolbar, null, null, []));
    }

    [Fact]
    public void IsBulkAction_IsACompatibilityAliasForTheMultipleScope()
    {
        var action = new CrudAction<TestProduct> { IsBulkAction = true };

        Assert.Equal(CrudActionScope.Multiple, action.Scope);

        action.Scope = CrudActionScope.Single;
        Assert.False(action.IsBulkAction);

        action.Scope = CrudActionScope.Multiple;
        Assert.True(action.IsBulkAction);

        action.IsBulkAction = false;
        Assert.Equal(CrudActionScope.None, action.Scope);

        var single = new CrudAction<TestProduct> { Scope = CrudActionScope.Single };
        single.IsBulkAction = false;
        Assert.Equal(CrudActionScope.Single, single.Scope);
    }

    [Fact]
    public void Contexts_DefaultFollowsTheScopeAndAnExplicitValueWins()
    {
        const CrudActionContext itemBound = CrudActionContext.All;
        const CrudActionContext itemFree = CrudActionContext.ListToolbar | CrudActionContext.Card | CrudActionContext.ListPart;

        Assert.Equal(itemFree, new CrudAction<TestProduct>().Contexts);
        Assert.Equal(itemBound, new CrudAction<TestProduct> { Scope = CrudActionScope.Single }.Contexts);
        Assert.Equal(itemBound, new CrudAction<TestProduct> { IsBulkAction = true }.Contexts);

        var explicitAction = new CrudAction<TestProduct> { Scope = CrudActionScope.Single, Contexts = CrudActionContext.ListToolbar };
        Assert.Equal(CrudActionContext.ListToolbar, explicitAction.Contexts);
        Assert.True(explicitAction.HasExplicitContexts);

        var changedScope = new CrudAction<TestProduct>();
        changedScope.Scope = CrudActionScope.Multiple;
        Assert.Equal(itemBound, changedScope.Contexts);
        Assert.False(changedScope.HasExplicitContexts);

        var noneExplicit = new CrudAction<TestProduct> { Contexts = CrudActionContext.All };
        Assert.Equal(CrudActionContext.All, noneExplicit.Contexts);
    }

    [Fact]
    public void Builder_SetsScopeEnabledAndReasons()
    {
        var configuration = new BaseListBuilder<TestProduct>()
            .ActionGroup("Process", group => group
                .Action("Run", a => a.SingleAction().Enabled(p => p.IsActive).DisabledReason("Inactive"))
                .Action("Stop", a => a.Scope(CrudActionScope.Multiple).DisabledReason(p => p.Name)))
            .Build();

        var actions = configuration.ActionGroups.Single().Actions;

        Assert.Equal(CrudActionScope.Single, actions[0].Scope);
        Assert.NotNull(actions[0].Enabled);
        Assert.Equal("Inactive", actions[0].DisabledReason);
        Assert.Equal(CrudActionScope.Multiple, actions[1].Scope);
        Assert.True(actions[1].IsBulkAction);
        Assert.Equal("X", actions[1].DisabledReasonProvider!(new TestProduct { Name = "X" }));
    }

    private static string Describe(CrudAction<TestProduct> action, List<TestProduct> targets) =>
        CrudActionEvaluator<TestProduct>.DescribeBlock(action, CrudActionEvaluator<TestProduct>.Evaluate(action, targets), Framework, t => t ?? string.Empty)!;

    private static string Framework(string key) => key switch
    {
        "ActionSelectRowFirst" => "Select a row first",
        "ActionSelectRowsFirst" => "Select one or more rows first",
        "ActionSelectExactlyOne" => "Select exactly one row",
        "ActionNotPossibleForRow" => "Not possible for the selected row",
        "ActionNotPossibleForRows" => "Not possible for {0} of {1} selected rows",
        _ => key
    };

    private sealed class ServiceCollectionStub : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
