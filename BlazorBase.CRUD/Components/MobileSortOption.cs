namespace BlazorBase.CRUD.Components;

/// <summary>
/// One entry of the sort selector a list shows above its tiles.
/// </summary>
/// <param name="PropertyName">The property the list sorts by; empty for the list's own default order.</param>
/// <param name="Title">The text of the entry.</param>
public sealed record MobileSortOption(string PropertyName, string Title);
