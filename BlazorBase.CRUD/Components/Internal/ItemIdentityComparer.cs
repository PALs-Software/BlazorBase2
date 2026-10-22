using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BlazorBase.CRUD.Components.Internal;

/// <summary>
/// Compares list rows by their <c>Id</c> property, and by reference when a model has none or the id is not set.
/// </summary>
/// <remarks>
/// A virtualized list drops rows it holds when they are far off screen and fetches them again, as new instances, when
/// they scroll back into view. A selection kept by reference would lose such a row; kept by id it does not.
/// </remarks>
internal sealed class ItemIdentityComparer<TModel> : IEqualityComparer<TModel> where TModel : class
{
    private static readonly PropertyInfo? IdProperty = typeof(TModel).GetProperty("Id");

    public static ItemIdentityComparer<TModel> Instance { get; } = new();

    public bool Equals(TModel? left, TModel? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        var leftId = ReadId(left);

        return leftId is not null && leftId.Equals(ReadId(right));
    }

    public int GetHashCode(TModel item) => ReadId(item)?.GetHashCode() ?? RuntimeHelpers.GetHashCode(item);

    private static object? ReadId(TModel item)
    {
        var id = IdProperty?.GetValue(item);

        if (id is null)
            return null;

        return id.Equals(DefaultOf(id.GetType())) ? null : id;
    }

    private static readonly ConcurrentDictionary<Type, object?> Defaults = new();

    private static object? DefaultOf(Type type)
        => type.IsValueType ? Defaults.GetOrAdd(type, Activator.CreateInstance) : null;
}
