namespace BlazorBase.Components.Help;

/// <summary>One field tooltip: what the script shows for a label of the page.</summary>
/// <param name="Key">The normalized label, see <see cref="HelpLabelKey.Normalize"/>; the script compares the visible text of an element with it.</param>
/// <param name="Label">The label as written in the help.</param>
/// <param name="Html">The first sentence of the entry as safe inline HTML.</param>
/// <param name="Anchor">The id of the entry in the rendered topic, the target of the "More in help" link.</param>
public sealed record HelpFieldTip(string Key, string Label, string Html, string Anchor);
