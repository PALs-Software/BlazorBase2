using System.Security.Claims;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Components.Internal;

/// <summary>
/// Why an item-bound action cannot run right now.
/// </summary>
internal enum CrudActionBlock
{
    None,
    NoSelection,
    NeedsExactlyOne,
    ItemsBlocked
}

/// <summary>
/// The outcome of checking one action against the rows it would work on.
/// </summary>
internal sealed class CrudActionState<TModel> where TModel : class
{
    public CrudActionBlock Block { get; init; }

    public bool IsEnabled => Block == CrudActionBlock.None;

    /// <summary>
    /// The rows the action would work on.
    /// </summary>
    public IReadOnlyList<TModel> Targets { get; init; } = [];

    /// <summary>
    /// The rows whose <see cref="CrudAction{TModel}.Enabled"/> predicate failed.
    /// </summary>
    public IReadOnlyList<TModel> BlockingItems { get; init; } = [];

    /// <summary>
    /// The distinct reasons the blocking rows gave, unresolved (they may be resource keys).
    /// </summary>
    public IReadOnlyList<string> ItemReasons { get; init; } = [];
}

/// <summary>
/// The rules of the action model, free of any rendering: which rows an action works on, when it is enabled, why it is
/// not, and when it is visible. The toolbar and the row menu both ask this class so they cannot disagree.
/// </summary>
internal static class CrudActionEvaluator<TModel> where TModel : class
{
    /// <summary>
    /// The rows an action in a toolbar or card works on: the selection, or the single current item when nothing is selected.
    /// </summary>
    public static IReadOnlyList<TModel> ResolveSelection(TModel? item, IReadOnlyCollection<TModel>? selectedItems)
    {
        if (selectedItems is { Count: > 0 })
            return [.. selectedItems];

        return item is not null ? [item] : [];
    }

    /// <summary>
    /// The rows an action in the row menu works on when the user opened the menu on <paramref name="clicked"/>:
    /// a multi-row action takes the whole selection when the clicked row belongs to it, anything else only the clicked row.
    /// </summary>
    public static IReadOnlyList<TModel> ResolveRowMenuTargets(
        CrudAction<TModel> action,
        TModel clicked,
        IReadOnlyCollection<TModel> selectedItems,
        Func<TModel, bool> isSelected)
    {
        if (action.Scope == CrudActionScope.Multiple && selectedItems.Count > 1 && isSelected(clicked))
            return [.. selectedItems];

        return [clicked];
    }

    /// <summary>
    /// Checks <paramref name="action"/> against the rows it would work on.
    /// </summary>
    public static CrudActionState<TModel> Evaluate(CrudAction<TModel> action, IReadOnlyList<TModel> targets)
    {
        var block = action.Scope switch
        {
            CrudActionScope.Single when targets.Count == 0 => CrudActionBlock.NoSelection,
            CrudActionScope.Single when targets.Count > 1 => CrudActionBlock.NeedsExactlyOne,
            CrudActionScope.Multiple when targets.Count == 0 => CrudActionBlock.NoSelection,
            _ => CrudActionBlock.None
        };

        if (block != CrudActionBlock.None)
            return new CrudActionState<TModel> { Block = block, Targets = targets };

        if (action.Enabled is null)
            return new CrudActionState<TModel> { Targets = targets };

        var blocking = targets.Where(item => !action.Enabled(item)).ToList();

        if (blocking.Count == 0)
            return new CrudActionState<TModel> { Targets = targets };

        return new CrudActionState<TModel>
        {
            Block = CrudActionBlock.ItemsBlocked,
            Targets = targets,
            BlockingItems = blocking,
            ItemReasons = CollectReasons(action, blocking)
        };
    }

    /// <summary>
    /// The text that tells the user why the action is disabled, or null while it is enabled.
    /// </summary>
    /// <param name="action">The action the state belongs to.</param>
    /// <param name="state">The state to explain.</param>
    /// <param name="framework">Resolves a framework string by key.</param>
    /// <param name="resolve">Resolves an action text (a caption or reason that may be a resource key).</param>
    public static string? DescribeBlock(
        CrudAction<TModel> action,
        CrudActionState<TModel> state,
        Func<string, string> framework,
        Func<string?, string> resolve)
    {
        switch (state.Block)
        {
            case CrudActionBlock.None:
                return null;

            case CrudActionBlock.NoSelection:
                return framework(action.Scope == CrudActionScope.Multiple ? "ActionSelectRowsFirst" : "ActionSelectRowFirst");

            case CrudActionBlock.NeedsExactlyOne:
                return framework("ActionSelectExactlyOne");
        }

        var head = state.Targets.Count == 1
            ? framework("ActionNotPossibleForRow")
            : string.Format(framework("ActionNotPossibleForRows"), state.BlockingItems.Count, state.Targets.Count);

        var reasons = state.ItemReasons
            .Select(reason => resolve(reason))
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .ToList();

        return reasons.Count == 0 ? head : $"{head}: {string.Join("; ", reasons)}";
    }

    /// <summary>
    /// Builds the event arguments an action is run with.
    /// </summary>
    public static CrudActionEventArgs<TModel> BuildEventArgs(
        CrudAction<TModel> action,
        IReadOnlyList<TModel> targets,
        TModel? item,
        IServiceProvider serviceProvider,
        CrudActionContext context)
    {
        return action.Scope switch
        {
            CrudActionScope.Single => new CrudActionEventArgs<TModel>
            {
                Item = targets.Count == 1 ? targets[0] : item,
                SelectedItems = targets,
                ServiceProvider = serviceProvider,
                Context = context
            },
            CrudActionScope.Multiple => new CrudActionEventArgs<TModel>
            {
                Item = item,
                SelectedItems = targets,
                ServiceProvider = serviceProvider,
                Context = context
            },
            _ => new CrudActionEventArgs<TModel>
            {
                Item = item,
                SelectedItems = item is not null ? [item] : [],
                ServiceProvider = serviceProvider,
                Context = context
            }
        };
    }

    /// <summary>
    /// Whether the action is offered in <paramref name="context"/>: its contexts, the roles and the async
    /// <see cref="CrudAction{TModel}.Visible"/> predicate, evaluated with the rows in <paramref name="targets"/>.
    /// </summary>
    /// <remarks>
    /// The predicate is always asked, so a decision that does not depend on a row (a feature switch) hides an
    /// item-bound action even while nothing is selected. <see cref="CrudActionVisibilityArgs{TModel}.Item"/> is the
    /// row the decision is about and null when there is none (nothing selected, or a single-row action with several
    /// rows selected); with several rows a multi-row action is asked once per row and is visible only when the
    /// predicate holds for every one of them.
    /// </remarks>
    public static async Task<bool> IsVisibleAsync(
        CrudAction<TModel> action,
        CrudActionContext context,
        ClaimsPrincipal? user,
        TModel? item,
        IReadOnlyList<TModel> targets)
    {
        if (!action.Contexts.HasFlag(context))
            return false;

        if (action.VisibleForRoles is not null && user is not null && !action.VisibleForRoles(user))
            return false;

        if (action.Visible is null)
            return true;

        switch (action.Scope)
        {
            case CrudActionScope.Single:
                return await action.Visible(CreateVisibilityArgs(context, user, targets.Count == 1 ? targets[0] : null, targets));

            case CrudActionScope.Multiple:
                if (targets.Count == 0)
                    return await action.Visible(CreateVisibilityArgs(context, user, null, targets));

                foreach (var target in targets)
                {
                    if (!await action.Visible(CreateVisibilityArgs(context, user, target, targets)))
                        return false;
                }

                return true;

            default:
                return await action.Visible(CreateVisibilityArgs(context, user, targets.Count == 1 ? targets[0] : item, targets));
        }
    }

    /// <summary>
    /// Whether the group is offered: its roles and its async <see cref="CrudActionGroup{TModel}.Visible"/> predicate.
    /// </summary>
    public static async Task<bool> IsGroupVisibleAsync(
        CrudActionGroup<TModel> group,
        CrudActionContext context,
        ClaimsPrincipal? user,
        TModel? item,
        IReadOnlyList<TModel> targets)
    {
        if (!group.Contexts.HasFlag(context))
            return false;

        if (group.VisibleForRoles is not null && user is not null && !group.VisibleForRoles(user))
            return false;

        if (group.Visible is null)
            return true;

        return await group.Visible(CreateVisibilityArgs(context, user, targets.Count == 1 ? targets[0] : item, targets));
    }

    private static CrudActionVisibilityArgs<TModel> CreateVisibilityArgs(
        CrudActionContext context,
        ClaimsPrincipal? user,
        TModel? item,
        IReadOnlyList<TModel> targets) => new()
        {
            Item = item,
            SelectedItems = targets,
            User = user,
            Context = context
        };

    private static List<string> CollectReasons(CrudAction<TModel> action, List<TModel> blocking)
    {
        var reasons = new List<string>();

        foreach (var item in blocking)
        {
            var reason = action.DisabledReasonProvider?.Invoke(item);

            if (string.IsNullOrWhiteSpace(reason))
                reason = action.DisabledReason;

            if (!string.IsNullOrWhiteSpace(reason) && !reasons.Contains(reason))
                reasons.Add(reason);
        }

        return reasons;
    }
}
