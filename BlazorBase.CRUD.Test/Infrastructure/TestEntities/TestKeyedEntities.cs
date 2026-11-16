using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>Key by EF convention: <c>&lt;TypeName&gt;Id</c>, no attribute, no <c>Id</c> property.</summary>
[CrudAccess("Admin", "RIMD")]
public class TestTypeNameKeyed
{
    public Guid TestTypeNameKeyedId { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>Key declared by <see cref="KeyAttribute"/> on a property that is neither <c>Id</c> nor <c>&lt;TypeName&gt;Id</c>.</summary>
[CrudAccess("Admin", "RIMD")]
public class TestAttributeKeyed
{
    [Key]
    public int Code { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>Carries <c>Id</c> next to a declared key: <c>Id</c> keeps identifying the row.</summary>
[CrudAccess("Admin", "RIMD")]
public class TestAttributeKeyedWithDecoyId
{
    [Key]
    public Guid Reference { get; set; }

    public string Id { get; set; } = string.Empty;
}

/// <summary>Composite key: no single key property resolves.</summary>
public class TestCompositeKeyed
{
    [Key]
    public int First { get; set; }

    [Key]
    public int Second { get; set; }
}

/// <summary>No key at all.</summary>
public class TestKeyless
{
    public string Name { get; set; } = string.Empty;
}
