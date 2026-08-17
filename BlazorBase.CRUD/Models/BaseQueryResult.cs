namespace BlazorBase.CRUD.Models;

/// <summary>
/// Paged result returned by data providers.
/// </summary>
public class BaseQueryResult<T>
{
    public List<T> Items { get; set; } = [];

    public int TotalCount { get; set; }
}
