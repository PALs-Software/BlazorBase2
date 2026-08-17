using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Benchmarks.Entities;

[BaseCrud("bench-orders")]
public class BenchOrder
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<BenchOrderItem> Items { get; set; } = [];
}
