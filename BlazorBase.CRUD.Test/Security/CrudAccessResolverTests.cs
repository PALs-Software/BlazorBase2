using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Security;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Security;

public class CrudAccessResolverTests
{
    [Fact]
    public void EvaluateClass_Admin_GetsAllRights()
    {
        var rights = CrudAccessResolver.EvaluateClass(typeof(TestProduct), TestPrincipals.WithRoles("Admin"));

        Assert.Equal(CrudRights.All, rights);
    }

    [Fact]
    public void EvaluateClass_User_GetsOnlyRead()
    {
        var rights = CrudAccessResolver.EvaluateClass(typeof(TestProduct), TestPrincipals.WithRoles("User"));

        Assert.Equal(CrudRights.Read, rights);
    }

    [Fact]
    public void EvaluateClass_UnmatchedRole_GetsNone()
    {
        var rights = CrudAccessResolver.EvaluateClass(typeof(TestProduct), TestPrincipals.Anonymous());

        Assert.Equal(CrudRights.None, rights);
    }

    [Fact]
    public void EvaluateClass_MultipleRoles_UnionsRights()
    {
        var rights = CrudAccessResolver.EvaluateClass(typeof(TestProduct), TestPrincipals.WithRoles("Admin", "User"));

        Assert.Equal(CrudRights.All, rights);
    }

    [Fact]
    public void EvaluateClass_NoAttributes_GetsAllRights()
    {
        var rights = CrudAccessResolver.EvaluateClass(typeof(TestReview), TestPrincipals.Anonymous());

        Assert.Equal(CrudRights.All, rights);
    }

    [Fact]
    public void EvaluateClass_Wildcard_MatchesEveryone()
    {
        var rights = CrudAccessResolver.EvaluateClass(typeof(WildcardEntity), TestPrincipals.Anonymous());

        Assert.Equal(CrudRights.Read, rights);
    }

    [Fact]
    public void EvaluateProperty_IntersectsClassAndPropertyRights()
    {
        var rights = CrudAccessResolver.EvaluateProperty(typeof(TestProduct), "CostPrice", TestPrincipals.WithRoles("Admin"));

        Assert.Equal(CrudRights.Read | CrudRights.Modify, rights);
    }

    [Fact]
    public void EvaluateProperty_RoleMatchingPropertyButNotClass_GetsNarrowedRights()
    {
        var rights = CrudAccessResolver.EvaluateProperty(typeof(TestProduct), "CostPrice", TestPrincipals.WithRoles("Manager"));

        Assert.Equal(CrudRights.Read, rights);
    }

    [Fact]
    public void EvaluateProperty_RoleWithoutPropertyAccess_GetsNone()
    {
        var rights = CrudAccessResolver.EvaluateProperty(typeof(TestProduct), "CostPrice", TestPrincipals.WithRoles("User"));

        Assert.Equal(CrudRights.None, rights);
    }

    [Fact]
    public void EvaluateProperty_UnannotatedProperty_FallsBackToClassRights()
    {
        var rights = CrudAccessResolver.EvaluateProperty(typeof(TestProduct), "Name", TestPrincipals.WithRoles("Admin"));

        Assert.Equal(CrudRights.All, rights);
    }

    [Fact]
    public void IsAlwaysExcluded_TrueForZeroRightWildcardProperty()
    {
        Assert.True(CrudAccessResolver.IsAlwaysExcluded(typeof(TestProduct), "InternalNotes"));
    }

    [Fact]
    public void IsAlwaysExcluded_FalseForGrantingProperty()
    {
        Assert.False(CrudAccessResolver.IsAlwaysExcluded(typeof(TestProduct), "CostPrice"));
    }

    [Fact]
    public void IsAlwaysExcluded_FalseForUnannotatedProperty()
    {
        Assert.False(CrudAccessResolver.IsAlwaysExcluded(typeof(TestProduct), "Name"));
    }

    [Fact]
    public void CollectDeclaredRoles_ReturnsDistinctRolesWithoutWildcard()
    {
        var roles = CrudAccessResolver.CollectDeclaredRoles(typeof(TestProduct));

        Assert.Equal(3, roles.Count);
        Assert.Contains("Admin", roles);
        Assert.Contains("Manager", roles);
        Assert.Contains("User", roles);
        Assert.DoesNotContain("*", roles);
    }

    [Fact]
    public void ParseRule_ParsesRolesAndRights()
    {
        var rule = CrudAccessResolver.ParseRule("Admin, User", "RM", allowDelete: true);

        Assert.Equal(["Admin", "User"], rule.Roles);
        Assert.False(rule.IsWildcard);
        Assert.Equal(CrudRights.Read | CrudRights.Modify, rule.Rights);
    }

    [Fact]
    public void ParseRule_DetectsWildcard()
    {
        var rule = CrudAccessResolver.ParseRule("*", "R", allowDelete: true);

        Assert.True(rule.IsWildcard);
    }

    [Fact]
    public void ParseRule_InvalidRightsCharacter_Throws()
    {
        Assert.Throws<ArgumentException>(() => CrudAccessResolver.ParseRule("Admin", "Z", allowDelete: true));
    }

    [Fact]
    public void ParseRule_DeleteOnNonDeletableTarget_Throws()
    {
        Assert.Throws<ArgumentException>(() => CrudAccessResolver.ParseRule("Admin", "D", allowDelete: false));
    }

    [Fact]
    public void ParseRule_EmptyRoles_Throws()
    {
        Assert.Throws<ArgumentException>(() => CrudAccessResolver.ParseRule("", "R", allowDelete: true));
    }

    [Fact]
    public void DeleteRightOnScalarProperty_ThrowsDuringResolution()
    {
        Assert.Throws<ArgumentException>(
            () => CrudAccessResolver.GetPropertyRules(typeof(InvalidDeleteEntity), "Secret"));
    }

    [Fact]
    public void ApplyViewRights_NullRules_ReturnsUpperBoundUnchanged()
    {
        var rights = CrudAccessResolver.ApplyViewRights(CrudRights.All, null, TestPrincipals.WithRoles("Admin"));

        Assert.Equal(CrudRights.All, rights);
    }

    [Fact]
    public void ApplyViewRights_FurtherRestrictsUpperBound()
    {
        var viewRules = new[] { CrudAccessResolver.ParseRule("Admin", "R", allowDelete: false) };

        var rights = CrudAccessResolver.ApplyViewRights(CrudRights.All, viewRules, TestPrincipals.WithRoles("Admin"));

        Assert.Equal(CrudRights.Read, rights);
    }

    [Fact]
    public void EvaluateNavigationTarget_IntersectsOwnerPropertyAndTargetClass()
    {
        var rights = CrudAccessResolver.EvaluateNavigationTarget(
            typeof(TestProduct), "Reviews", typeof(TestReview), TestPrincipals.WithRoles("User"));

        Assert.Equal(CrudRights.Read, rights);
    }

    [CrudAccess("*", "R")]
    private class WildcardEntity
    {
        public Guid Id { get; set; }
    }

    private class InvalidDeleteEntity
    {
        public Guid Id { get; set; }

        [CrudAccess("Admin", "D")]
        public string Secret { get; set; } = string.Empty;
    }
}
