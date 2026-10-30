using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// Entity dedicated to the PATCH typed-value tests: exercises a string, a bool, a nullable
/// enum, a nullable <see cref="Guid"/>, a nullable <see cref="decimal"/> and a nullable
/// <see cref="DateTime"/> property so <see cref="BlazorBase.CRUD.DataProviders.PatchFieldValueConverter"/>
/// and the interceptor pipeline can be proven against every representative shape.
/// </summary>
[BaseCrud("patchtargets")]
public class TestPatchTarget
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsFlagged { get; set; }

    public ProductKind? Kind { get; set; }

    public Guid? ReferenceId { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? OccurredOn { get; set; }
}
