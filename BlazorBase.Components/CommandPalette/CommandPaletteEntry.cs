using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.CommandPalette;

/// <summary>
/// One result a <see cref="BaseCommandPalette"/> can offer: a page to open, an app to switch to or an action to run.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="Href"/> and <see cref="Execute"/> must be set; an entry that sets neither or both is
/// dropped by the palette and logged. Texts are shown as given, so they have to be localized by the provider.
/// </remarks>
public sealed record CommandPaletteEntry
{
    /// <summary>Stable identifier, unique across all providers. It is the key of the "recently used" list; the first entry with an id wins.</summary>
    public required string Id { get; init; }

    /// <summary>The name of the entry, shown in the row and searched first.</summary>
    public required string Title { get; init; }

    /// <summary>Optional second line, for example the breadcrumb of a page. It is searched with a lower priority than the title.</summary>
    public string? Subtitle { get; init; }

    /// <summary>The already localized heading of the group the entry is listed under, for example "Pages", "Actions" or "Apps".</summary>
    public required string Group { get; init; }

    /// <summary>Position of the group among the others; lower values come first, equal values are ordered by the group name.</summary>
    public int GroupOrder { get; init; }

    /// <summary>
    /// Position of the entry within its group; lower values come first. Entries without a value follow the ones
    /// with a value, in the order the providers delivered them, and equal values keep the delivery order. With an
    /// empty query it decides the order of the suggestions; while typing it breaks ties between entries with the
    /// same match score.
    /// </summary>
    public int? Order { get; init; }

    /// <summary>
    /// The address to open. A relative address (or an absolute one below the application's base address) is
    /// navigated to inside the application; any other absolute http, https, mailto or tel address opens in a new tab.
    /// </summary>
    public string? Href { get; init; }

    /// <summary>The action to run when the entry is chosen. Exceptions are caught, logged and shown as a toast.</summary>
    public Func<Task>? Execute { get; init; }

    /// <summary>A Fluent UI icon for the tile in front of the row.</summary>
    public Icon? Icon { get; init; }

    /// <summary>The source of an image for the tile in front of the row; used when <see cref="Icon"/> is null.</summary>
    public string? IconSrc { get; init; }

    /// <summary>Additional search terms that are not shown, for example synonyms.</summary>
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>
    /// Comma separated roles of which the user needs at least one; null offers the entry to everybody. This is the
    /// central permission gate: the palette drops every entry whose role the current user lacks, whatever provider
    /// returned it. Evaluated like <c>NavigationItem.RequiredRole</c>.
    /// </summary>
    public string? RequiredRole { get; init; }

    /// <summary>Opens <see cref="Href"/> in a new browser tab (with <c>noopener noreferrer</c>) even when it points into the application.</summary>
    public bool OpenInNewTab { get; init; }

    /// <summary>
    /// The already localized kind shown at the end of the row, for example "Page" or "App". When null the palette
    /// shows "Action" for an entry with <see cref="Execute"/>, "Link" for an address outside the application and
    /// "Page" otherwise.
    /// </summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Set by a provider that matched the entry against the query itself (a server side search, for example fuzzy or
    /// full text). The entry is then listed even if the query does not occur in its title, subtitle or keywords; it
    /// ranks below the entries that match the text.
    /// </summary>
    public bool PreMatched { get; init; }

    internal bool HasValidTarget => (Href is null) != (Execute is null);
}
