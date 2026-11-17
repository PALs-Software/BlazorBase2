using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.User.Components.UserMenu;

/// <summary>
/// A host-provided entry of <see cref="BaseUserMenu"/>, listed between the theme choice and "Sign out" — for
/// example "Settings" or "Profile".
/// </summary>
/// <remarks>
/// Give an entry either <see cref="Href"/> (rendered as a link) or <see cref="OnClick"/> (rendered as a button);
/// when both are set the link wins. The menu closes after the entry was chosen.
/// </remarks>
public sealed record UserMenuItem
{
    /// <summary>Unique within the menu; keeps the entry stable across renders.</summary>
    public required string Key { get; init; }

    /// <summary>Already localized display text.</summary>
    public required string Label { get; init; }

    /// <summary>The address to navigate to, relative to the app's base address (for example <c>settings</c>).</summary>
    public string? Href { get; init; }

    /// <summary>An optional Fluent UI icon in front of the label.</summary>
    public Icon? Icon { get; init; }

    /// <summary>An action run instead of navigating.</summary>
    public Func<Task>? OnClick { get; init; }
}
