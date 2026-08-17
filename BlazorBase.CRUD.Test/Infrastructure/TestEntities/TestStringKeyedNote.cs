using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// Entity whose key is a string initialised to <see cref="string.Empty"/>, the shape ASP.NET
/// Identity uses. Exists so the "is this model new" decision can be tested for a key whose unset
/// value is not the CLR default — comparing it against <c>null</c> made every new instance look
/// like an existing row.
/// </summary>
[BaseCrud("string-keyed-notes")]
[CrudAccess("Admin", "RIMD")]
public class TestStringKeyedNote
{
    public string Id { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Text { get; set; } = string.Empty;
}
