using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// Navigation-lookup target entity restricted to the "Admin" role at class level, used to verify
/// that <see cref="NavigationAccessOwner"/>'s navigation lookup respects the target type's own
/// <see cref="CrudAccessAttribute"/> rather than only the owner's rights (CRUD-UI-02).
/// </summary>
[CrudAccess("Admin", "RIMD")]
public class NavigationAccessTarget
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
