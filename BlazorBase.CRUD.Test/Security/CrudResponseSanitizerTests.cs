using BlazorBase.CRUD.Security;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Security.Entities;
using Xunit;

namespace BlazorBase.CRUD.Test.Security;

public class CrudResponseSanitizerTests
{
    [Fact]
    public void Strip_Anonymous_StripsRestrictedScalarAndNavigationButRetainsUnrestrictedFields()
    {
        var entity = new SecuredEntity
        {
            Id = 1,
            Name = "Alice",
            Salary = 5000m,
            Secret = new SecretDetail { Id = 1, Value = "TopSecret" },
            Public = new PublicDetail { Info = "General info", RestrictedInfo = "Restricted info" }
        };

        CrudResponseSanitizer.Strip(entity, TestPrincipals.Anonymous());

        Assert.Equal(0m, entity.Salary);
        Assert.Null(entity.Secret);
        Assert.Equal(1, entity.Id);
        Assert.Equal("Alice", entity.Name);
        Assert.NotNull(entity.Public);
        Assert.Null(entity.Public!.RestrictedInfo);
        Assert.Equal("General info", entity.Public.Info);
    }

    [Fact]
    public void Strip_Admin_RetainsAllFieldsIncludingRestrictedScalarAndNavigations()
    {
        var entity = new SecuredEntity
        {
            Id = 1,
            Name = "Alice",
            Salary = 5000m,
            Secret = new SecretDetail { Id = 1, Value = "TopSecret" },
            Public = new PublicDetail { Info = "General info", RestrictedInfo = "Restricted info" }
        };

        CrudResponseSanitizer.Strip(entity, TestPrincipals.WithRoles("Admin"));

        Assert.Equal(5000m, entity.Salary);
        Assert.NotNull(entity.Secret);
        Assert.Equal("TopSecret", entity.Secret!.Value);
        Assert.NotNull(entity.Public);
        Assert.Equal("Restricted info", entity.Public!.RestrictedInfo);
    }

    [Fact]
    public void Strip_Anonymous_StripsRestrictedFieldOnNonIdComplexReferenceButRetainsUnrestrictedFields()
    {
        var entity = new SecuredEntity
        {
            Id = 1,
            Name = "Alice",
            Compensation = new Compensation { Currency = "EUR", Amount = 100m }
        };

        CrudResponseSanitizer.Strip(entity, TestPrincipals.Anonymous());

        Assert.NotNull(entity.Compensation);
        Assert.Equal(0m, entity.Compensation!.Amount);
        Assert.Equal("EUR", entity.Compensation.Currency);
    }

    [Fact]
    public void Strip_Admin_RetainsRestrictedFieldOnNonIdComplexReference()
    {
        var entity = new SecuredEntity
        {
            Id = 1,
            Name = "Alice",
            Compensation = new Compensation { Currency = "EUR", Amount = 100m }
        };

        CrudResponseSanitizer.Strip(entity, TestPrincipals.WithRoles("Admin"));

        Assert.NotNull(entity.Compensation);
        Assert.Equal(100m, entity.Compensation!.Amount);
    }

    [Fact]
    public void Strip_CyclicObjectGraph_DoesNotCauseInfiniteRecursion()
    {
        var entity = new SecuredEntity
        {
            Id = 1,
            Name = "Alice",
            Salary = 5000m
        };
        entity.Related = entity;

        CrudResponseSanitizer.Strip(entity, TestPrincipals.Anonymous());

        Assert.Equal(0m, entity.Salary);
    }
}
