using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// A child entity that names itself through an ordered pair of display keys, the shape a real
/// list-part row usually has (a code plus a human-readable name).
/// </summary>
public class TestPrinting
{
    public Guid Id { get; set; }

    [DisplayKey(Order = 0)]
    public string? Code { get; set; }

    [DisplayKey(Order = 1)]
    public string? SetName { get; set; }

    public int Quantity { get; set; }
}
