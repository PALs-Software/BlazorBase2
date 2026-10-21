using System.Linq.Expressions;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// One property a list's tile shows, declared through <see cref="MobileCardBuilder{TModel}"/>.
/// </summary>
public class MobileFieldConfig<TModel>
{
    public required Expression<Func<TModel, object?>> Property { get; set; }

    public string PropertyName { get; internal set; } = String.Empty;

    public MobileRole Role { get; set; } = MobileRole.Field;

    /// <summary>Position among the properties of the same role; the declaration order unless set otherwise.</summary>
    public int Priority { get; set; }

    /// <summary>The label shown beside the value; the column title or the localized property name when null.</summary>
    public string? Label { get; set; }
}
