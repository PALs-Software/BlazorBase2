namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Request body of the "set default" endpoint. A null <see cref="Id"/> clears the user's default for the list.
/// </summary>
public sealed class SetListFilterDefaultRequest
{
    public Guid? Id { get; set; }
}
