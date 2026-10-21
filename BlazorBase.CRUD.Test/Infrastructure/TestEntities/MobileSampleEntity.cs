namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// Entity with one property of every kind the automatic tile choice distinguishes, in a deliberately
/// unhelpful order: identifiers first, long data last.
/// </summary>
public class MobileSampleEntity
{
    public Guid Id { get; set; }

    public Guid? TeamId { get; set; }

    public int CustomerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    public ProductKind Kind { get; set; }

    public decimal Amount { get; set; }

    public int Count { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? LastSeenOn { get; set; }

    public byte[]? Payload { get; set; }

    public ICollection<string> Tags { get; set; } = [];

    public TestCategory? Category { get; set; }
}
