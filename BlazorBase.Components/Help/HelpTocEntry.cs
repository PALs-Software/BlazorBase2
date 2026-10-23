namespace BlazorBase.Components.Help;

/// <summary>An entry of the table of contents: a level-2 heading (or a level-3 heading that has none above it) with the level-3 headings below it.</summary>
/// <param name="Heading">The heading.</param>
/// <param name="Children">The level-3 headings up to the next level-2 heading.</param>
public sealed record HelpTocEntry(HelpHeading Heading, IReadOnlyList<HelpHeading> Children);
