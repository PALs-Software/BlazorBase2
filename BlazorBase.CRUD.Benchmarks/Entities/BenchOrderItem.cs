using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Benchmarks.Entities;

[BaseCrud("bench-order-items")]
public class BenchOrderItem
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public BenchOrder? Order { get; set; }

    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
