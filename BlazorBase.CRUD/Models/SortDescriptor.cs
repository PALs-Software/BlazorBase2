namespace BlazorBase.CRUD.Models;

/// <summary>
/// Describes a sort condition on a property.
/// </summary>
public class SortDescriptor
{
    public required string PropertyName { get; set; }

    public SortDirection Direction { get; set; } = SortDirection.Ascending;
}
