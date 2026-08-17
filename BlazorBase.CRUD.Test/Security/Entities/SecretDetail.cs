using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Security.Entities;

/// <summary>
/// Reference-navigation target whose class-level <see cref="CrudAccessAttribute"/>
/// restricts every operation to the "Admin" role.
/// </summary>
[CrudAccess("Admin", "RIMD")]
public class SecretDetail
{
    public int Id { get; set; }

    public string? Value { get; set; }
}
