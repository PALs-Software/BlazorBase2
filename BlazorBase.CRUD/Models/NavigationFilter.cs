namespace BlazorBase.CRUD.Models;

/// <summary>
/// Describes filters and sorts to apply to a navigation collection include.
/// Enables EF Core filtered includes like <c>.Include(e => e.Items.Where(...).OrderBy(...))</c>.
/// </summary>
public class NavigationFilter
{
    public required string NavigationName { get; set; }

    public List<FilterDescriptor> Filters { get; set; } = [];

    public List<SortDescriptor> Sorts { get; set; } = [];
}
