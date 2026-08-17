using System;

namespace BlazorBase.CRUD.Attributes;

/// <summary>
/// Marks a property as a display key — a human-readable identifier shown in the
/// BaseCard header and used as the FK lookup display text, instead of the raw
/// primary key. Multiple properties may carry the attribute; they are combined
/// in ascending <see cref="Order"/> to form the displayed identity (useful when
/// the primary key is a GUID or other value that reads poorly for humans).
/// </summary>
/// <remarks>
/// Explicit card-level configuration (fluent <c>BaseCardBuilder</c> or markup
/// <see cref="Components.DisplayKeyField{TModel}"/> / <c>PropertyField</c> flag)
/// takes precedence over this attribute; the attribute in turn takes precedence
/// over the primary key. Precedence is "most specific wins", never a merge.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DisplayKeyAttribute : Attribute
{
    /// <summary>
    /// Position of this property within the combined display string. Lower values
    /// appear first. Properties sharing an order fall back to declaration order.
    /// </summary>
    public int Order { get; set; }
}
