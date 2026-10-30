namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Thrown when a list key, filter name or filter tree is rejected by <see cref="ListFilterValidator"/>.
/// </summary>
public class ListFilterValidationException(string message) : Exception(message);
