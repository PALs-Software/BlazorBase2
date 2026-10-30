namespace BlazorBase.CRUD.Attributes;

/// <summary>
/// Declares that a <see cref="DateTime"/> property holds a UTC instant even though it carries no
/// <see cref="DateTimeKind"/> (a value read from a database or deserialized from JSON without an offset is
/// <see cref="DateTimeKind.Unspecified"/>), so a list shows it in the viewer's local time.
/// </summary>
/// <remarks>
/// The audit fields <c>CreatedOn</c> and <c>ModifiedOn</c> of <see cref="Core.AuditModel"/> are UTC by convention and
/// need no attribute. A property without this attribute and without a kind is shown exactly as stored, which is what
/// a date-only value (a month, a due date) needs.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class UtcDateTimeAttribute : Attribute;
