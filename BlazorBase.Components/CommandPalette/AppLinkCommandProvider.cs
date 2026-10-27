using BlazorBase.Components.Layout.AppSwitcher;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Components.CommandPalette;

/// <summary>
/// Offers the links of the app switcher (<see cref="AppLinkGroup"/>) to the command palette under "Apps", with the
/// title of their group as subtitle.
/// </summary>
/// <remarks>
/// <c>BaseLayout</c> uses it automatically for <c>BaseLayout.AppLinks</c>; construct it yourself only to use the
/// links without the layout. Links and groups the current user may not see are not returned.
/// </remarks>
public sealed class AppLinkCommandProvider(IReadOnlyList<AppLinkGroup> groups, IStringLocalizer<BaseCommandPalette> localizer) : ICommandPaletteProvider
{
    /// <summary>The position of the "Apps" group.</summary>
    public int GroupOrder { get; init; } = 200;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<CommandPaletteEntry>> GetEntriesAsync(CommandPaletteRequest request, CancellationToken cancellationToken)
    {
        var groupLabel = localizer["GroupApps"].Value;
        var hint = localizer["HintApp"].Value;
        var entries = new List<CommandPaletteEntry>();

        foreach (var group in AppLinkVisibility.Filter(groups, request.User))
        {
            foreach (var link in group.Links)
            {
                entries.Add(new CommandPaletteEntry
                {
                    Id = $"app:{group.Key}/{link.Key}",
                    Title = link.Label,
                    Subtitle = group.Label,
                    Group = groupLabel,
                    GroupOrder = GroupOrder,
                    Href = link.Href,
                    IconSrc = link.IconSrc,
                    Keywords = string.IsNullOrWhiteSpace(link.Description) ? null : [link.Description],
                    RequiredRole = link.RequiredRole,
                    OpenInNewTab = link.OpenInNewTab,
                    Hint = hint,
                    Order = entries.Count,
                });
            }
        }

        return ValueTask.FromResult<IReadOnlyList<CommandPaletteEntry>>(entries);
    }
}
