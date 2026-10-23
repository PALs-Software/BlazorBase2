namespace BlazorBase.Components.Help;

/// <summary>A heading of a help document with the id it gets in the rendered page.</summary>
/// <param name="Level">The heading level, 1 to 6.</param>
/// <param name="Text">The plain text of the heading.</param>
/// <param name="Id">The unique slug that is the <c>id</c> attribute of the rendered heading.</param>
public sealed record HelpHeading(int Level, string Text, string Id);
