using BlazorBase.Components.Layout.Navigation;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Components.CommandPalette;

/// <summary>
/// Offers the pages and actions of an application's <see cref="NavigationItem"/> tree to the command palette.
/// </summary>
/// <remarks>
/// <c>BaseLayout</c> uses it automatically for the items it is given; construct it yourself only to use the tree
/// without the layout. Groups are flattened: a page is listed under "Pages" with its breadcrumb as subtitle (for
/// example "Administration › Users"), an entry with <see cref="NavigationItem.OnClick"/> under "Actions". Entries
/// the current user may not see (<see cref="NavigationItem.RequiredRole"/> of the entry or any parent) are not
/// returned, and groups without a target are skipped. The entries carry <see cref="CommandPaletteEntry.Order"/> in the
/// depth-first position of the tree, so the palette lists them as the navigation shows them.
/// </remarks>
public sealed class NavigationCommandProvider(IReadOnlyList<NavigationItem> items, IStringLocalizer<BaseCommandPalette> localizer) : ICommandPaletteProvider
{
    private const string BreadcrumbSeparator = " › ";

    /// <summary>The position of the "Pages" group.</summary>
    public int PagesGroupOrder { get; init; } = 100;

    /// <summary>The position of the "Actions" group.</summary>
    public int ActionsGroupOrder { get; init; } = 300;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<CommandPaletteEntry>> GetEntriesAsync(CommandPaletteRequest request, CancellationToken cancellationToken)
    {
        var entries = new List<CommandPaletteEntry>();
        Flatten(NavigationVisibility.Filter(items, request.User), [], entries);
        return ValueTask.FromResult<IReadOnlyList<CommandPaletteEntry>>(entries);
    }

    private void Flatten(IReadOnlyList<NavigationItem> level, IReadOnlyList<NavigationItem> parents, List<CommandPaletteEntry> entries)
    {
        foreach (var item in level)
        {
            if (item.IsGroup)
            {
                Flatten(item.Children, [.. parents, item], entries);
                continue;
            }

            var entry = ToEntry(item, parents);
            if (entry is not null)
                entries.Add(entry with { Order = entries.Count });
        }
    }

    private CommandPaletteEntry? ToEntry(NavigationItem item, IReadOnlyList<NavigationItem> parents)
    {
        var subtitle = parents.Count == 0 ? null : string.Join(BreadcrumbSeparator, parents.Select(parent => parent.Label));
        var keywords = string.IsNullOrWhiteSpace(item.ShortLabel) || item.ShortLabel == item.Label ? null : new[] { item.ShortLabel };

        if (item.IsAction)
        {
            return new CommandPaletteEntry
            {
                Id = "action:" + string.Join('/', parents.Append(item).Select(entry => entry.StateKey)),
                Title = item.Label,
                Subtitle = subtitle,
                Group = localizer["GroupActions"],
                GroupOrder = ActionsGroupOrder,
                Execute = item.OnClick,
                Icon = item.Icon,
                Keywords = keywords,
                RequiredRole = item.RequiredRole,
                Hint = localizer["HintAction"],
            };
        }

        if (string.IsNullOrEmpty(item.Href))
            return null;

        return new CommandPaletteEntry
        {
            Id = "page:" + item.Href,
            Title = item.Label,
            Subtitle = subtitle,
            Group = localizer["GroupPages"],
            GroupOrder = PagesGroupOrder,
            Href = item.Href,
            Icon = item.Icon,
            Keywords = keywords,
            RequiredRole = item.RequiredRole,
            Hint = localizer["HintPage"],
        };
    }
}
