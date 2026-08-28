using BlazorBase.CRUD.Security;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Security;

/// <summary>
/// Covers <see cref="CrudAccessResolver.StrictMode"/>: off by default (today's permissive
/// behavior for an unannotated entity), and denies everything once a host opts in. Shares
/// <see cref="CrudAccessResolverCollection"/> with <see cref="CrudAccessResolverTests"/> so the
/// two never run concurrently against the same process-global flag.
/// </summary>
[Collection(CrudAccessResolverCollection.Name)]
public sealed class CrudAccessResolverStrictModeTests
{
    [Fact]
    public void EvaluateClass_UnannotatedEntity_PermissiveByDefault_GetsAllRights()
    {
        Assert.False(CrudAccessResolver.StrictMode);

        var rights = CrudAccessResolver.EvaluateClass(typeof(TestReview), TestPrincipals.Anonymous());

        Assert.Equal(CrudRights.All, rights);
    }

    [Fact]
    public void EvaluateClass_UnannotatedEntity_StrictModeOn_GetsNoRights()
    {
        CrudAccessResolver.StrictMode = true;
        try
        {
            var rights = CrudAccessResolver.EvaluateClass(typeof(TestReview), TestPrincipals.WithRoles("Admin"));

            Assert.Equal(CrudRights.None, rights);
        }
        finally
        {
            CrudAccessResolver.StrictMode = false;
        }
    }

    [Fact]
    public void EvaluateClass_AnnotatedEntity_StrictModeOn_StillEvaluatesItsOwnRules()
    {
        CrudAccessResolver.StrictMode = true;
        try
        {
            var rights = CrudAccessResolver.EvaluateClass(typeof(TestProduct), TestPrincipals.WithRoles("Admin"));

            Assert.Equal(CrudRights.All, rights);
        }
        finally
        {
            CrudAccessResolver.StrictMode = false;
        }
    }
}
