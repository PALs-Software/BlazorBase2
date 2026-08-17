using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using Xunit;

namespace BlazorBase.CRUD.Test.Filtering;

public class FilterOperatorCatalogTests
{
    [Fact]
    public void Text_OffersStringOperators_AndNullChecks()
    {
        var operators = FilterOperatorCatalog.GetDefaultOperators(FilterFieldKind.Text, isNullable: true);

        Assert.Contains(FilterOperator.Contains, operators);
        Assert.Contains(FilterOperator.StartsWith, operators);
        Assert.Contains(FilterOperator.Equals, operators);
        Assert.Contains(FilterOperator.IsNull, operators);
        Assert.DoesNotContain(FilterOperator.GreaterThan, operators);
    }

    [Fact]
    public void Number_OffersComparisons_NoContains_NoNullChecksWhenNotNullable()
    {
        var operators = FilterOperatorCatalog.GetDefaultOperators(FilterFieldKind.Number, isNullable: false);

        Assert.Contains(FilterOperator.GreaterThan, operators);
        Assert.Contains(FilterOperator.LessThanOrEqual, operators);
        Assert.DoesNotContain(FilterOperator.Contains, operators);
        Assert.DoesNotContain(FilterOperator.IsNull, operators);
    }

    [Fact]
    public void Nullable_AddsNullChecks()
    {
        var operators = FilterOperatorCatalog.GetDefaultOperators(FilterFieldKind.Number, isNullable: true);

        Assert.Contains(FilterOperator.IsNull, operators);
        Assert.Contains(FilterOperator.IsNotNull, operators);
    }

    [Fact]
    public void Boolean_OffersEqualsOnly()
    {
        var operators = FilterOperatorCatalog.GetDefaultOperators(FilterFieldKind.Boolean, isNullable: false);

        Assert.Equal([FilterOperator.Equals], operators);
    }

    [Fact]
    public void Enum_OffersEqualityOperators()
    {
        var operators = FilterOperatorCatalog.GetDefaultOperators(FilterFieldKind.Enum, isNullable: false);

        Assert.Contains(FilterOperator.Equals, operators);
        Assert.Contains(FilterOperator.NotEquals, operators);
        Assert.DoesNotContain(FilterOperator.GreaterThan, operators);
    }
}
