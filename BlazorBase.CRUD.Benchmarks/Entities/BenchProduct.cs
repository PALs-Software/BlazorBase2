using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Benchmarks.Entities;

[BaseCrud("bench-products")]
public class BenchProduct
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int Stock { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CategoryId { get; set; }

    public BenchCategory? Category { get; set; }
}
