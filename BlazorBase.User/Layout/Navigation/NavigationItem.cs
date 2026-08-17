using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.User.Layout.Navigation;

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

    /// <summary>Whether this entry appears as a primary tab in the mobile bottom bar; otherwise it lands in the "More" sheet.</summary>
    public bool IsMobilePrimary { get; init; }

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
