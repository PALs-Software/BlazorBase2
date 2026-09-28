using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Security;
using Xunit;

namespace BlazorBase.CRUD.Test.Security;

public sealed class CrudAccessAnnotationValidatorTests
{
    [BaseCrud("annotated")]
    [CrudAccess("Admin", "R")]
    private sealed class AnnotatedEntity;

    [BaseCrud("unannotated")]
    private sealed class UnannotatedEntity;

    [Fact]
    public void FindUnannotatedEntities_OnlyAnnotatedCandidates_ReturnsEmpty()
    {
        Assert.Empty(CrudAccessAnnotationValidator.FindUnannotatedEntities([typeof(AnnotatedEntity)]));
    }

    [Fact]
    public void FindUnannotatedEntities_EntityWithoutClassLevelCrudAccess_IsReported()
    {
        var unannotated = CrudAccessAnnotationValidator.FindUnannotatedEntities(
            [typeof(AnnotatedEntity), typeof(UnannotatedEntity)]);

        Assert.Equal([nameof(UnannotatedEntity)], unannotated);
    }

    [Fact]
    public async Task StartAsync_ScansAnAssemblyWithAnUnannotatedBaseCrudEntity_Throws()
    {
        var validator = new CrudAccessAnnotationValidator(typeof(UnannotatedEntity).Assembly);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(UnannotatedEntity), exception.Message, StringComparison.Ordinal);
    }
}
