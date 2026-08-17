namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// Owner entity carrying no <see cref="BlazorBase.CRUD.Attributes.CrudAccessAttribute"/> of its own,
/// with a reference navigation to <see cref="NavigationAccessTarget"/>. Isolates the CRUD-UI-02
/// navigation-lookup read guard: any denial observed for this owner must come from the target
/// type's own access rule, not from the owner's.
/// </summary>
public class NavigationAccessOwner
{
    public Guid Id { get; set; }

    public Guid? TargetId { get; set; }

    public NavigationAccessTarget? Target { get; set; }
}
