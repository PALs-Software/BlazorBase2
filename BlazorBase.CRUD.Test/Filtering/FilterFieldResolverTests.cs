using System.Security.Claims;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Filtering;

public class FilterFieldResolverTests
{
    private static List<FilterFieldMetadata> Resolve(FilterConfiguration<TestProduct> configuration, ClaimsPrincipal? user = null)
        => FilterFieldResolver.Resolve(configuration, user, EmptyLocalizer.Instance);

    [Fact]
    public void Auto_NoUser_IncludesScalarFields_ExcludesNavigations()
    {
        var names = Resolve(new FilterConfiguration<TestProduct>()).Select(f => f.PropertyName).ToList();

        Assert.Contains("Name", names);
        Assert.Contains("Price", names);
        Assert.Contains("Kind", names);
        Assert.Contains("CategoryId", names);
        Assert.DoesNotContain("Category", names);
        Assert.DoesNotContain("Reviews", names);
    }

    [Fact]
    public void Auto_AdminUser_ExcludesUnreadableField()
    {
        var names = Resolve(new FilterConfiguration<TestProduct>(), TestPrincipals.WithRoles("Admin"))
            .Select(f => f.PropertyName)
            .ToList();

        Assert.DoesNotContain("InternalNotes", names);
        Assert.Contains("CostPrice", names);
    }

    [Fact]
    public void EnumField_CarriesEnumNames()
    {
        var kind = Resolve(new FilterConfiguration<TestProduct>()).Single(f => f.PropertyName == "Kind");

        Assert.Equal(FilterFieldKind.Enum, kind.Kind);
        Assert.Equal(["Physical", "Digital", "Service"], kind.EnumNames);
    }

    [Fact]
    public void Include_ReturnsOnlyListedFields()
    {
        var configuration = new FilterConfigBuilder<TestProduct>().Include(p => p.Name).Build();

        Assert.Equal(["Name"], Resolve(configuration).Select(f => f.PropertyName));
    }

    [Fact]
    public void Exclude_RemovesListedFields()
    {
        var configuration = new FilterConfigBuilder<TestProduct>().Exclude(p => p.Price).Build();
        var names = Resolve(configuration).Select(f => f.PropertyName).ToList();

        Assert.DoesNotContain("Price", names);
        Assert.Contains("Name", names);
    }

    [Fact]
    public void Disabled_ReturnsEmpty()
    {
        var configuration = new FilterConfigBuilder<TestProduct>().Disable().Build();

        Assert.Empty(Resolve(configuration));
    }

    [Fact]
    public void Override_AppliesLabelAndOperators()
    {
        var configuration = new FilterConfigBuilder<TestProduct>()
            .Field(p => p.Name, f => f.Label("Bezeichnung").Operators(FilterOperator.Equals))
            .Build();

        var name = Resolve(configuration).Single(f => f.PropertyName == "Name");

        Assert.Equal("Bezeichnung", name.Label);
        Assert.Equal([FilterOperator.Equals], name.Operators);
    }

    [Fact]
    public void MixingIncludeAndExclude_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new FilterConfigBuilder<TestProduct>().Include(p => p.Name).Exclude(p => p.Price));
    }
}
