namespace BlazorBase.CRUD.Models;

/// <summary>
/// Global server-side cap on <see cref="BaseQuery.Take"/>, enforced by
/// <see cref="DataProviders.DbContextBaseDataProvider{TEntity}"/> before materializing a page of
/// results. Protects both the REST-endpoint path and the direct Blazor-Server component path
/// against a client requesting an unbounded page (e.g. <c>Take = int.MaxValue</c>).
/// </summary>
/// <remarks>
/// Set once at startup, before any query executes. The cap cannot be disabled by assigning a
/// non-positive value — a mis-bound configuration key of <c>0</c> keeps the default of
/// <c>1000</c> instead of silently exposing an unbounded page. A host that genuinely needs to
/// remove the cap sets <see cref="int.MaxValue"/> explicitly.
/// </remarks>
public static class BaseQueryLimits
{
    private const int DefaultMaxPageSize = 1000;

    private static int MaxPageSizeValue = DefaultMaxPageSize;

    public static int MaxPageSize
    {
        get => MaxPageSizeValue;
        set => MaxPageSizeValue = value > 0 ? value : DefaultMaxPageSize;
    }
}
