using System.ComponentModel.DataAnnotations.Schema;
using BlazorBase.CRUD.Navigation;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Navigation;

public class NavigationPropertyResolverTests
{
    [Fact]
    public void ReferenceNavigation_IsDetectedWithConventionalForeignKey()
    {
        var category = NavigationPropertyResolver.GetNavigationProperty(typeof(TestProduct), "Category");

        Assert.NotNull(category);
        Assert.False(category!.IsCollection);
        Assert.Equal("CategoryId", category.ForeignKeyPropertyName);
        Assert.Equal(typeof(TestCategory), category.TargetType);
    }

    [Fact]
    public void NavigationLookup_IsCaseInsensitive()
    {
        var category = NavigationPropertyResolver.GetNavigationProperty(typeof(TestProduct), "category");

        Assert.NotNull(category);
        Assert.Equal("Category", category!.PropertyName);
    }

    [Fact]
    public void CollectionNavigation_IsDetectedWithoutForeignKey()
    {
        var reviews = NavigationPropertyResolver.GetNavigationProperty(typeof(TestProduct), "Reviews");

        Assert.NotNull(reviews);
        Assert.True(reviews!.IsCollection);
        Assert.Null(reviews.ForeignKeyPropertyName);
        Assert.Equal(typeof(TestReview), reviews.TargetType);
    }

    [Fact]
    public void ScalarProperties_AreNotNavigations()
    {
        var navigations = NavigationPropertyResolver.GetNavigationProperties(typeof(TestProduct));

        Assert.DoesNotContain(navigations, n => n.PropertyName == "Name");
        Assert.DoesNotContain(navigations, n => n.PropertyName == "Price");
        Assert.DoesNotContain(navigations, n => n.PropertyName == "ReleasedOn");
        Assert.DoesNotContain(navigations, n => n.PropertyName == "Kind");
        Assert.DoesNotContain(navigations, n => n.PropertyName == "CategoryId");
    }

    [Fact]
    public void GetForeignKeyProperty_ReturnsPropertyForReference()
    {
        var fk = NavigationPropertyResolver.GetForeignKeyProperty(typeof(TestProduct), "Category");

        Assert.NotNull(fk);
        Assert.Equal("CategoryId", fk!.Name);
    }

    [Fact]
    public void GetForeignKeyProperty_ReturnsNullForCollection()
    {
        var fk = NavigationPropertyResolver.GetForeignKeyProperty(typeof(TestProduct), "Reviews");

        Assert.Null(fk);
    }

    [Fact]
    public void ForeignKeyAttribute_OverridesConvention()
    {
        var nav = NavigationPropertyResolver.GetNavigationProperty(typeof(ForeignKeyEntity), "Owner");

        Assert.NotNull(nav);
        Assert.Equal("OwnerRef", nav!.ForeignKeyPropertyName);
    }

    [Fact]
    public void UpperCaseIdConvention_IsResolved()
    {
        var nav = NavigationPropertyResolver.GetNavigationProperty(typeof(UpperIdEntity), "Thing");

        Assert.NotNull(nav);
        Assert.Equal("ThingID", nav!.ForeignKeyPropertyName);
    }

    [Fact]
    public void ReferenceWithoutMatchingForeignKey_HasNullForeignKey()
    {
        var nav = NavigationPropertyResolver.GetNavigationProperty(typeof(OrphanNavEntity), "Loose");

        Assert.NotNull(nav);
        Assert.Null(nav!.ForeignKeyPropertyName);
        Assert.Null(NavigationPropertyResolver.GetForeignKeyProperty(typeof(OrphanNavEntity), "Loose"));
    }

    [Fact]
    public void ClassWithoutIdProperty_IsNotANavigation()
    {
        var navigations = NavigationPropertyResolver.GetNavigationProperties(typeof(HolderEntity));

        Assert.DoesNotContain(navigations, n => n.PropertyName == "Value");
    }

    private class ForeignKeyEntity
    {
        public Guid Id { get; set; }

        [ForeignKey("OwnerRef")]
        public TestCategory Owner { get; set; } = default!;

        public Guid OwnerRef { get; set; }
    }

    private class UpperIdEntity
    {
        public Guid Id { get; set; }

        public TestCategory Thing { get; set; } = default!;

        public Guid ThingID { get; set; }
    }

    private class OrphanNavEntity
    {
        public Guid Id { get; set; }

        public TestCategory Loose { get; set; } = default!;
    }

    private class NoIdValue
    {
        public string Label { get; set; } = string.Empty;
    }

    private class HolderEntity
    {
        public Guid Id { get; set; }

        public NoIdValue Value { get; set; } = default!;
    }
}
