using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Layout.Navigation;

/// <summary>
/// One entry in an application's primary navigation. The same list of entries drives both
/// <see cref="BaseSideNavigation"/> (desktop) and <see cref="BaseBottomNavigation"/> (mobile);
/// only the visual rendering differs, so the two presentations can never diverge.
/// </summary>
public sealed record NavigationItem
{
    /// <summary>Target route for a plain link. Null for groups and actions.</summary>
    public string? Href { get; init; }

    /// <summary>Already-localized display text.</summary>
    public required string Label { get; init; }

    /// <summary>Optional Fluent UI icon shown next to the label.</summary>
    public Icon? Icon { get; init; }

    /// <summary>Match the route in full (<c>NavLinkMatch.All</c>) instead of by prefix — e.g. the "/" home link.</summary>
    public bool MatchAll { get; init; }

    /// <summary>When set, the entry is only shown to users in this role.</summary>
    public string? RequiredRole { get; init; }

    /// <summary>
    /// Whether this entry appears as a quick link in the mobile bottom bar, wherever it sits in the tree;
    /// otherwise it is only reachable through "More". When no entry is flagged the bar falls back to the first
    /// top-level links.
    /// </summary>
    public bool IsMobilePrimary { get; init; }

    /// <summary>
    /// The entry exists for the phone only and is never listed in <see cref="BaseSideNavigation"/> on wide
    /// screens. On a phone it is a bottom bar tab when <see cref="IsMobilePrimary"/> selects it; when it does not
    /// make it into the bar (more tabs than the bar holds) it is listed in "More" — the built-in sheet of
    /// <see cref="BaseBottomNavigation"/> or the navigation drawer of <see cref="BaseLayout"/> — so it never
    /// vanishes from the phone menu. A flagged group hides its whole subtree from the side navigation.
    /// </summary>
    public bool BottomBarOnly { get; init; }

    /// <summary>Position among the bottom bar tabs; lower values come first, equal values keep the order of the list.</summary>
    public int BottomBarOrder { get; init; }

    /// <summary>Shorter text for the bottom bar tab, where a tab only has room for a word or two. Falls back to <see cref="Label"/>.</summary>
    public string? ShortLabel { get; init; }

    /// <summary>
    /// Stable identity of a group that survives a language change, used to remember whether the group is
    /// expanded. Falls back to <see cref="Label"/>, which is localized and so only stable within one language.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>The key a group is remembered under.</summary>
    public string StateKey => Key ?? Label;

    /// <summary>Renders the entry with a destructive accent (e.g. logout).</summary>
    public bool IsDanger { get; init; }

    /// <summary>Action invoked on click instead of navigating. Used for entries such as logout.</summary>
    public Func<Task>? OnClick { get; init; }

    /// <summary>Child entries. A non-empty list turns this entry into a group that reveals its children instead of navigating.</summary>
    public IReadOnlyList<NavigationItem> Children { get; init; } = [];

    /// <summary>True when the entry groups child entries rather than being a direct destination.</summary>
    public bool IsGroup => Children.Count > 0;

    /// <summary>True when the entry triggers an action rather than navigating.</summary>
    public bool IsAction => OnClick is not null;
}
