using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Core;

namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// Audit-tracked entity used for audit-field population and optimistic-concurrency tests.
/// Inherits <see cref="AuditModel"/>, whose <c>ModifiedOn</c> carries <c>[ConcurrencyCheck]</c>.
/// </summary>
[BaseCrud("orders")]
public class TestOrder : AuditModel
{
    public Guid Id { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public decimal Total { get; set; }
}
