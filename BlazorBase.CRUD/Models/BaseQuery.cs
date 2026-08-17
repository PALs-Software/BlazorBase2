namespace BlazorBase.CRUD.Models;

/// <summary>
/// Describes a data query with filtering, sorting, pagination, and optional field selection.
/// </summary>
public class BaseQuery
{
    public List<FilterDescriptor> Filters { get; set; } = [];

    public List<SortDescriptor> Sorts { get; set; } = [];

    public List<string>? Select { get; set; }

    public List<NavigationFilter>? NavigationFilters { get; set; }

    public int Skip { get; set; }

    public int Take { get; set; } = 50;
}
