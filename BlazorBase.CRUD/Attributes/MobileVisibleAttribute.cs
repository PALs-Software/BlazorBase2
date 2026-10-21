using BlazorBase.CRUD.Configuration;

namespace BlazorBase.CRUD.Attributes;

/// <summary>
/// Declares on the entity which part a property plays on the tile a list shows on a narrow screen. A role set
/// on the list column (<c>BaseColumn.MobileRole</c>, <c>ColumnBuilder.Mobile</c>) or in the list's
/// <c>MobileCard</c> configuration takes precedence over this attribute.
/// </summary>
/// <remarks>
/// As soon as one property of a list carries an explicit role, the tile shows only the properties with a role:
/// everything undecided is left off. Without any role the list chooses by data type.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class MobileVisibleAttribute : Attribute
{
    /// <summary>Marks a property as not set, see <see cref="Order"/>.</summary>
    public const int Unordered = Int32.MaxValue;

    /// <summary>What the property is on the tile. A plain label and value pair by default.</summary>
    public MobileRole Role { get; set; } = MobileRole.Field;

    /// <summary>Position among the properties of the same role; lower values come first.</summary>
    public int Order { get; set; } = Unordered;
}
