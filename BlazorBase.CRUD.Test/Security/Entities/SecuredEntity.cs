using System.Collections.Generic;
using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Security.Entities;

/// <summary>
/// Test entity exercising field- and navigation-level <see cref="CrudAccessAttribute"/> rules.
/// Id, Name, Public, Children and Related are unrestricted; Salary and Secret require the "Admin" role.
/// </summary>
public class SecuredEntity
{
    public int Id { get; set; }

    public string? Name { get; set; }

    [CrudAccess("Admin", "RIM")]
    public decimal Salary { get; set; }

    [CrudAccess("Admin", "RIMD")]
    public SecretDetail? Secret { get; set; }

    public PublicDetail? Public { get; set; }

    public SecuredEntity? Related { get; set; }

    public ICollection<SecuredChild> Children { get; set; } = new List<SecuredChild>();

    public Compensation? Compensation { get; set; }
}
