using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Querying;

namespace BlazorBase.CRUD.DataProviders;

/// <summary>
/// Read-only <see cref="IBaseDataProvider{TModel}"/> over an in-memory list supplied by a live
/// delegate, for hosts whose own caller already computed the full list to display (e.g.
/// precomputed statistics).
/// </summary>
/// <remarks>
/// The query's filters and sorts are evaluated against the list: <c>Contains</c>, <c>StartsWith</c>,
/// <c>EndsWith</c>, <c>Equals</c>, <c>NotEquals</c>, the four range operators, <c>IsNull</c>,
/// <c>IsNotNull</c> and <c>In</c>, in nested AND/OR groups, on dotted property paths. Text comparison
/// ignores case; a date filter without a time of day matches the whole day. A condition whose property
/// does not exist or whose value cannot be converted is ignored, like the database provider does.
/// <see cref="BaseQuery.Skip"/> and <see cref="BaseQuery.Take"/> are deliberately not applied: the whole
/// matching list is returned as a single page and <see cref="BaseQueryResult{T}.TotalCount"/> equals its
/// length, so a grid never asks for a further page and can never append the same rows twice.
/// </remarks>
public class InMemoryBaseDataProvider<T>(
    Func<IReadOnlyList<T>> itemsAccessor
) : IBaseDataProvider<T>
    where T : class
{
    #region Injects
    private readonly Func<IReadOnlyList<T>> ItemsAccessor = itemsAccessor ?? throw new ArgumentNullException(nameof(itemsAccessor));
    #endregion

    private static readonly PropertyInfo? IdProperty = typeof(T).GetProperty("Id");
    private static readonly ConcurrentDictionary<string, PropertyPath?> PropertyPaths = new(StringComparer.OrdinalIgnoreCase);

    public Task<BaseQueryResult<T>> GetListAsync(BaseQuery query, Expression<Func<T, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<T> items = ItemsAccessor();

        if (scopeFilter is not null)
            items = items.Where(scopeFilter.Compile());

        items = items.Where(item => query.Filters.All(filter => Evaluate(item, filter) ?? true));
        items = ApplySorts(items, query.Sorts);

        var matching = items.ToList();
        return Task.FromResult(new BaseQueryResult<T> { Items = matching, TotalCount = matching.Count });
    }

    public Task<T?> GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<T, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        if (IdProperty is null)
            return Task.FromResult<T?>(null);

        var match = ItemsAccessor().FirstOrDefault(item => Equals(IdProperty.GetValue(item), id));
        return Task.FromResult(match);
    }

    public Task<int> GetCountAsync(Expression<Func<T, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult(ItemsAccessor().Count);

    public Task<T> CreateAsync(T model, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(InMemoryBaseDataProvider<T>)} is read-only and does not support creating entries.");

    public Task<T> PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(InMemoryBaseDataProvider<T>)} is read-only and does not support patching entries.");

    public Task DeleteAsync(object id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(InMemoryBaseDataProvider<T>)} is read-only and does not support deleting entries.");

    #region Filtering

    /// <summary>
    /// Evaluates a filter against an item. Returns <c>null</c> when the filter is not applicable and must
    /// not take part in the result.
    /// </summary>
    private static bool? Evaluate(T item, FilterDescriptor filter)
    {
        if (filter.IsGroup)
            return EvaluateGroup(item, filter);

        if (String.IsNullOrEmpty(filter.PropertyName))
            return null;

        var path = ResolvePath(filter.PropertyName);

        if (path is null)
            return null;

        return EvaluateLeaf(path.GetValue(item), path.PropertyType, filter);
    }

    private static bool? EvaluateGroup(T item, FilterDescriptor group)
    {
        var results = group.Filters!
            .Select(child => Evaluate(item, child))
            .Where(result => result is not null)
            .Select(result => result!.Value)
            .ToList();

        if (results.Count == 0)
            return null;

        return group.Logic == FilterLogic.Or ? results.Any(result => result) : results.All(result => result);
    }

    private static bool? EvaluateLeaf(object? actual, Type propertyType, FilterDescriptor filter)
    {
        if (filter.Operator == FilterOperator.IsNull)
            return actual is null;

        if (filter.Operator == FilterOperator.IsNotNull)
            return actual is not null;

        if (filter.Value is null)
            return null;

        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (filter.Operator == FilterOperator.In)
            return EvaluateIn(actual, targetType, filter.Value);

        if (filter.Operator is FilterOperator.Contains or FilterOperator.StartsWith or FilterOperator.EndsWith)
            return EvaluateText(actual, filter);

        if (!TryCoerce(filter.Value, targetType, out var expected))
            return null;

        return EvaluateComparison(actual, expected, filter.Operator);
    }

    private static bool? EvaluateIn(object? actual, Type targetType, object rawValue)
    {
        IList? candidates;

        try
        {
            candidates = FilterValueCoercion.CoerceCollection(rawValue, targetType);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or ArgumentException or OverflowException)
        {
            return null;
        }

        if (candidates is null || candidates.Count == 0)
            return null;

        if (actual is null)
            return false;

        return candidates.Cast<object>().Any(candidate => AreEqual(actual, candidate));
    }

    private static bool? EvaluateText(object? actual, FilterDescriptor filter)
    {
        var expected = Convert.ToString(filter.Value, CultureInfo.CurrentCulture);

        if (String.IsNullOrEmpty(expected))
            return null;

        if (actual is null)
            return false;

        var text = Convert.ToString(actual, CultureInfo.CurrentCulture) ?? String.Empty;

        return filter.Operator switch
        {
            FilterOperator.Contains => text.Contains(expected, StringComparison.CurrentCultureIgnoreCase),
            FilterOperator.StartsWith => text.StartsWith(expected, StringComparison.CurrentCultureIgnoreCase),
            _ => text.EndsWith(expected, StringComparison.CurrentCultureIgnoreCase)
        };
    }

    private static bool? EvaluateComparison(object? actual, object expected, FilterOperator filterOperator)
    {
        if (filterOperator == FilterOperator.Equals)
            return actual is not null && AreEqual(actual, expected);

        if (filterOperator == FilterOperator.NotEquals)
            return actual is null || !AreEqual(actual, expected);

        if (actual is null)
            return false;

        var comparison = CompareValues(actual, expected);

        return filterOperator switch
        {
            FilterOperator.GreaterThan => comparison > 0,
            FilterOperator.LessThan => comparison < 0,
            FilterOperator.GreaterThanOrEqual => comparison >= 0,
            FilterOperator.LessThanOrEqual => comparison <= 0,
            _ => null
        };
    }

    private static bool TryCoerce(object rawValue, Type targetType, out object value)
    {
        try
        {
            var coerced = FilterValueCoercion.Coerce(rawValue, targetType);
            value = coerced!;
            return coerced is not null;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or ArgumentException or OverflowException)
        {
            value = null!;
            return false;
        }
    }

    private static bool AreEqual(object actual, object expected)
    {
        if (actual is string actualText && expected is string expectedText)
            return String.Equals(actualText, expectedText, StringComparison.CurrentCultureIgnoreCase);

        return CompareValues(actual, expected) == 0;
    }

    /// <summary>
    /// Orders two non-null values. When the expected value is a date without a time of day, the actual
    /// value is reduced to its date so a day filter matches the whole day.
    /// </summary>
    private static int CompareValues(object actual, object expected)
    {
        if (actual is DateTime actualDate && expected is DateTime { TimeOfDay.Ticks: 0 } expectedDate)
            return actualDate.Date.CompareTo(expectedDate);

        if (actual is DateTimeOffset actualOffset && expected is DateTimeOffset { TimeOfDay.Ticks: 0 } expectedOffset)
            return actualOffset.Date.CompareTo(expectedOffset.Date);

        return ValueComparer.Instance.Compare(actual, expected);
    }

    #endregion

    #region Sorting

    private static IEnumerable<T> ApplySorts(IEnumerable<T> items, List<SortDescriptor> sorts)
    {
        IOrderedEnumerable<T>? ordered = null;

        foreach (var sort in sorts)
        {
            var path = ResolvePath(sort.PropertyName);

            if (path is null)
                continue;

            var descending = sort.Direction == SortDirection.Descending;

            if (ordered is null)
                ordered = descending
                    ? items.OrderByDescending(path.GetValue, ValueComparer.Instance)
                    : items.OrderBy(path.GetValue, ValueComparer.Instance);
            else
                ordered = descending
                    ? ordered.ThenByDescending(path.GetValue, ValueComparer.Instance)
                    : ordered.ThenBy(path.GetValue, ValueComparer.Instance);
        }

        return ordered ?? items;
    }

    #endregion

    #region Property paths

    private static PropertyPath? ResolvePath(string propertyName)
        => PropertyPaths.GetOrAdd(propertyName, BuildPath);

    private static PropertyPath? BuildPath(string propertyName)
    {
        var properties = new List<PropertyInfo>();
        var currentType = typeof(T);

        foreach (var part in propertyName.Split('.'))
        {
            var property = currentType.GetProperty(part, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property is null || property.GetIndexParameters().Length > 0)
                return null;

            properties.Add(property);
            currentType = property.PropertyType;
        }

        return new PropertyPath([.. properties]);
    }

    private sealed class PropertyPath(PropertyInfo[] properties)
    {
        public Type PropertyType => properties[^1].PropertyType;

        public object? GetValue(T item)
        {
            object? current = item;

            foreach (var property in properties)
            {
                if (current is null)
                    return null;

                current = property.GetValue(current);
            }

            return current;
        }
    }

    private sealed class ValueComparer : IComparer<object?>
    {
        public static readonly ValueComparer Instance = new();

        public int Compare(object? left, object? right)
        {
            if (left is null)
                return right is null ? 0 : -1;

            if (right is null)
                return 1;

            if (left is string leftText && right is string rightText)
                return StringComparer.CurrentCultureIgnoreCase.Compare(leftText, rightText);

            if (left is IComparable comparable && left.GetType() == right.GetType())
                return comparable.CompareTo(right);

            return StringComparer.CurrentCultureIgnoreCase.Compare(
                Convert.ToString(left, CultureInfo.CurrentCulture),
                Convert.ToString(right, CultureInfo.CurrentCulture));
        }
    }

    #endregion
}
