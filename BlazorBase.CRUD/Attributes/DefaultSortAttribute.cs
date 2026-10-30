namespace BlazorBase.CRUD.Attributes;

/// <summary>
/// Declares the order a list shows the entity in until the user sorts it. May be repeated; the
/// declarations are applied in ascending <see cref="Order"/>.
/// </summary>
/// <remarks>
/// A list without a declared order falls back to the primary key, so rows never come back in an arbitrary
/// order and paging through them never skips or repeats one. An explicit <c>DefaultSorts</c> on the list or its
/// configuration takes precedence over this attribute.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class DefaultSortAttribute(string propertyName) : Attribute
{
    /// <summary>The property to sort by. A dotted path reaches into a reference navigation.</summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>Sorts from the highest value to the lowest.</summary>
    public bool Descending { get; set; }

    /// <summary>Position among several declarations; lower values sort first.</summary>
    public int Order { get; set; }
}
