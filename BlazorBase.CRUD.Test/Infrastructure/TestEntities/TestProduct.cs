using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// Representative entity exercising scalar types, an enum, a reference navigation
/// (Category via the CategoryId convention), a collection navigation (Reviews) and
/// class/property level [CrudAccess] rules for the security tests.
/// </summary>
[BaseCrud("products")]
[CrudAccess("Admin", "RIMD")]
[CrudAccess("Manager", "R")]
[CrudAccess("User", "R")]
public class TestProduct
{
    public Guid Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public bool IsActive { get; set; }

    public DateTime ReleasedOn { get; set; }

    public ProductKind Kind { get; set; }

    public string? Description { get; set; }

    [CrudAccess("Admin", "RM")]
    [CrudAccess("Manager", "R")]
    public decimal CostPrice { get; set; }

    [CrudAccess("*", "")]
    public string InternalNotes { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }

    public TestCategory? Category { get; set; }

    public ICollection<TestReview> Reviews { get; set; } = [];
}
