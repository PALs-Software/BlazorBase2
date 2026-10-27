namespace BlazorBase.Components.CommandPalette;

/// <summary>The "recently used" list of the command palette: newest first, no duplicates, at most a fixed number.</summary>
internal static class CommandPaletteRecents
{
    public static IReadOnlyList<string> Push(IReadOnlyList<string> current, string id, int max) =>
        [.. current.Where(existing => existing != id).Prepend(id).Take(Math.Max(max, 0))];
}
