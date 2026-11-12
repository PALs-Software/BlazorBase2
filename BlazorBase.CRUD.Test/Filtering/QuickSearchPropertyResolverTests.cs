using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Filtering;

public class QuickSearchPropertyResolverTests
{
    [Fact]
    public void TextProperties_AreKept_InTheSpellingTheModelDeclares()
    {
        var resolved = QuickSearchPropertyResolver.Resolve(typeof(TestProduct), ["name", "DESCRIPTION"], user: null);

        Assert.Equal(["Name", "Description"], resolved);
    }

    [Fact]
    public void PropertiesThatAreNotText_AreIgnored()
    {
        var resolved = QuickSearchPropertyResolver.Resolve(typeof(TestProduct), ["Name", "Price", "IsActive", "Kind", "Reviews"], user: null);

        Assert.Equal(["Name"], resolved);
    }

    [Fact]
    public void UnknownAndBlankPaths_AreIgnored()
    {
        var resolved = QuickSearchPropertyResolver.Resolve(typeof(TestProduct), ["Nope", "", "  ", "Category.Nope", "Name"], user: null);

        Assert.Equal(["Name"], resolved);
    }

    [Fact]
    public void ADottedPathIntoANavigationProperty_IsKept()
    {
        var resolved = QuickSearchPropertyResolver.Resolve(typeof(TestProduct), ["category.name"], user: null);

        Assert.Equal(["Category.Name"], resolved);
    }

    [Fact]
    public void DuplicatesAreCollapsed()
    {
        var resolved = QuickSearchPropertyResolver.Resolve(typeof(TestProduct), ["Name", "name"], user: null);

        Assert.Equal(["Name"], resolved);
    }

    [Fact]
    public void APropertyTheUserMayNotRead_IsIgnored()
    {
        var resolved = QuickSearchPropertyResolver.Resolve(typeof(TestProduct), ["Name", "InternalNotes"], TestPrincipals.WithRoles("Admin"));

        Assert.Equal(["Name"], resolved);
    }

    [Fact]
    public void WithoutAUser_EveryTextPropertyIsReadable()
    {
        var resolved = QuickSearchPropertyResolver.Resolve(typeof(TestProduct), ["Name", "InternalNotes"], user: null);

        Assert.Equal(["Name", "InternalNotes"], resolved);
    }
}
