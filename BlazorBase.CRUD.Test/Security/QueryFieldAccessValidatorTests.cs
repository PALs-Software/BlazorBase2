using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Security;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Security.Entities;
using Xunit;
using TestReview = BlazorBase.CRUD.Test.Infrastructure.TestEntities.TestReview;

namespace BlazorBase.CRUD.Test.Security;

public class QueryFieldAccessValidatorTests
{
    [Fact]
    public void FindUnreadableField_FilterOnUnrestrictedField_Anonymous_ReturnsNull()
    {
        var query = new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = "Name", Operator = FilterOperator.Equals, Value = "Alice" }]
        };

        var result = QueryFieldAccessValidator.FindUnreadableField(typeof(SecuredEntity), query, TestPrincipals.Anonymous());

        Assert.Null(result);
    }

    [Fact]
    public void FindUnreadableField_FilterOnRestrictedField_Anonymous_ReturnsFieldName()
    {
        var query = new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = "Salary", Operator = FilterOperator.GreaterThan, Value = 1000 }]
        };

        var result = QueryFieldAccessValidator.FindUnreadableField(typeof(SecuredEntity), query, TestPrincipals.Anonymous());

        Assert.Equal("Salary", result);
    }

    [Fact]
    public void FindUnreadableField_SortOnRestrictedField_Anonymous_ReturnsFieldName()
    {
        var query = new BaseQuery
        {
            Sorts = [new SortDescriptor { PropertyName = "Salary" }]
        };

        var result = QueryFieldAccessValidator.FindUnreadableField(typeof(SecuredEntity), query, TestPrincipals.Anonymous());

        Assert.Equal("Salary", result);
    }

    [Fact]
    public void FindUnreadableField_Admin_ReturnsNullForFilterAndSortOnRestrictedField()
    {
        var query = new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = "Salary", Operator = FilterOperator.GreaterThan, Value = 1000 }],
            Sorts = [new SortDescriptor { PropertyName = "Salary" }]
        };

        var result = QueryFieldAccessValidator.FindUnreadableField(typeof(SecuredEntity), query, TestPrincipals.WithRoles("Admin"));

        Assert.Null(result);
    }

    [Fact]
    public void FindUnreadableField_NavigationFilterOnRestrictedNavigation_Anonymous_ReturnsNavigationName()
    {
        var query = new BaseQuery
        {
            NavigationFilters =
            [
                new NavigationFilter
                {
                    NavigationName = "Secret",
                    Filters = [new FilterDescriptor { PropertyName = "Value", Operator = FilterOperator.Equals, Value = "x" }]
                }
            ]
        };

        var result = QueryFieldAccessValidator.FindUnreadableField(typeof(SecuredEntity), query, TestPrincipals.Anonymous());

        Assert.Equal("Secret", result);
    }

    [Fact]
    public void FindUnreadableField_NavigationFilterOnRestrictedNestedField_Anonymous_ReturnsNavigationDotFieldPath()
    {
        var query = new BaseQuery
        {
            NavigationFilters =
            [
                new NavigationFilter
                {
                    NavigationName = "Public",
                    Filters = [new FilterDescriptor { PropertyName = "RestrictedInfo", Operator = FilterOperator.Equals, Value = "x" }]
                }
            ]
        };

        var result = QueryFieldAccessValidator.FindUnreadableField(typeof(SecuredEntity), query, TestPrincipals.Anonymous());

        Assert.Equal("Public.RestrictedInfo", result);
    }

    [Fact]
    public void FindUnreadableField_FilterOnNonIdComplexReferenceRestrictedField_Anonymous_ReturnsNavigationDotFieldPath()
    {
        var query = new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = "Compensation.Amount", Operator = FilterOperator.GreaterThan, Value = 1000 }]
        };

        var result = QueryFieldAccessValidator.FindUnreadableField(typeof(SecuredEntity), query, TestPrincipals.Anonymous());

        Assert.Equal("Compensation.Amount", result);
    }

    [Fact]
    public void FindUnreadableField_FilterOnNonIdComplexReferenceRestrictedField_Admin_ReturnsNull()
    {
        var query = new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = "Compensation.Amount", Operator = FilterOperator.GreaterThan, Value = 1000 }]
        };

        var result = QueryFieldAccessValidator.FindUnreadableField(typeof(SecuredEntity), query, TestPrincipals.WithRoles("Admin"));

        Assert.Null(result);
    }

    [Fact]
    public void FindUnreadableField_UnknownFieldName_ReturnsNull()
    {
        var query = new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = "Nope", Operator = FilterOperator.Equals, Value = 1 }]
        };

        var result = QueryFieldAccessValidator.FindUnreadableField(typeof(SecuredEntity), query, TestPrincipals.Anonymous());

        Assert.Null(result);
    }

    [Fact]
    public void FindUnreadableField_UnannotatedModel_ReturnsNull()
    {
        var query = new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = "Author", Operator = FilterOperator.Equals, Value = "Alice" }]
        };

        var result = QueryFieldAccessValidator.FindUnreadableField(typeof(TestReview), query, TestPrincipals.Anonymous());

        Assert.Null(result);
    }
}
