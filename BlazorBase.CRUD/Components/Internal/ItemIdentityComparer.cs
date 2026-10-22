using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BlazorBase.CRUD.Components.Internal;

/// <summary>
/// Compares list rows by their identity: the <c>Id</c> property, otherwise the single <c>[Key]</c> property, and by
/// reference when a model has neither or the key is not set.
/// </summary>
/// <remarks>
/// A virtualized list drops rows it holds when they are far off screen and fetches them again, as new instances, when
/// they scroll back into view, and a refresh replaces every row. A selection or highlight kept by reference would lose
/// such a row; kept by key it does not. A composite key (several <c>[Key]</c> properties) is not used.
/// </remarks>
internal sealed class ItemIdentityComparer<TModel> : IEqualityComparer<TModel> where TModel : class
{
    private static readonly PropertyInfo? IdProperty = ResolveIdentityProperty();

    private static PropertyInfo? ResolveIdentityProperty()
    {
        var properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var id = properties.FirstOrDefault(property => property.Name == "Id" && property.GetIndexParameters().Length == 0);

        if (id is not null)
            return id;

        var keys = properties
            .Where(property => property.GetIndexParameters().Length == 0 && property.IsDefined(typeof(KeyAttribute), inherit: true))
            .ToList();

        return keys.Count == 1 ? keys[0] : null;
    }

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

        if (id is null || id is string { Length: 0 })
            return null;

        return id.Equals(DefaultOf(id.GetType())) ? null : id;
    }

    private static readonly ConcurrentDictionary<Type, object?> Defaults = new();

    private static object? DefaultOf(Type type)
        => type.IsValueType ? Defaults.GetOrAdd(type, Activator.CreateInstance) : null;
}
