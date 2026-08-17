using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Querying;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Querying;

public class FilterExpressionDecomposerTests
{
    [Fact]
    public void BoolMember_BecomesEqualsTrue()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.IsActive);

        Assert.False(result.IsGroup);
        Assert.Equal("IsActive", result.PropertyName);
        Assert.Equal(FilterOperator.Equals, result.Operator);
        Assert.True(Assert.IsType<bool>(result.Value));
    }

    [Fact]
    public void NegatedBoolMember_BecomesEqualsFalse()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => !p.IsActive);

        Assert.Equal("IsActive", result.PropertyName);
        Assert.Equal(FilterOperator.Equals, result.Operator);
        Assert.False(Assert.IsType<bool>(result.Value));
    }

    [Fact]
    public void StringEquality_BecomesEquals()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Name == "Widget");

        Assert.Equal("Name", result.PropertyName);
        Assert.Equal(FilterOperator.Equals, result.Operator);
        Assert.Equal("Widget", Assert.IsType<string>(result.Value));
    }

    [Fact]
    public void StringInequality_BecomesNotEquals()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Name != "Widget");

        Assert.Equal(FilterOperator.NotEquals, result.Operator);
        Assert.Equal("Widget", Assert.IsType<string>(result.Value));
    }

    [Theory]
    [InlineData(FilterOperator.GreaterThan)]
    [InlineData(FilterOperator.GreaterThanOrEqual)]
    [InlineData(FilterOperator.LessThan)]
    [InlineData(FilterOperator.LessThanOrEqual)]
    public void NumericComparisons_MapToOperators(FilterOperator expected)
    {
        var result = expected switch
        {
            FilterOperator.GreaterThan => FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Stock > 10),
            FilterOperator.GreaterThanOrEqual => FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Stock >= 10),
            FilterOperator.LessThan => FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Stock < 10),
            _ => FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Stock <= 10),
        };

        Assert.Equal("Stock", result.PropertyName);
        Assert.Equal(expected, result.Operator);
        Assert.Equal(10, Assert.IsType<int>(result.Value));
    }

    [Fact]
    public void NullEquality_BecomesIsNull()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Description == null);

        Assert.Equal("Description", result.PropertyName);
        Assert.Equal(FilterOperator.IsNull, result.Operator);
        Assert.Null(result.Value);
    }

    [Fact]
    public void NullInequality_BecomesIsNotNull()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Description != null);

        Assert.Equal(FilterOperator.IsNotNull, result.Operator);
    }

    [Fact]
    public void Contains_BecomesContainsOperator()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Name.Contains("dget"));

        Assert.Equal("Name", result.PropertyName);
        Assert.Equal(FilterOperator.Contains, result.Operator);
        Assert.Equal("dget", Assert.IsType<string>(result.Value));
    }

    [Fact]
    public void StartsWith_BecomesStartsWithOperator()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Name.StartsWith("Wid"));

        Assert.Equal(FilterOperator.StartsWith, result.Operator);
        Assert.Equal("Wid", Assert.IsType<string>(result.Value));
    }

    [Fact]
    public void EndsWith_BecomesEndsWithOperator()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Name.EndsWith("get"));

        Assert.Equal(FilterOperator.EndsWith, result.Operator);
        Assert.Equal("get", Assert.IsType<string>(result.Value));
    }

    [Fact]
    public void CapturedVariable_IsResolvedToItsValue()
    {
        var threshold = 7;

        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Stock == threshold);

        Assert.Equal(FilterOperator.Equals, result.Operator);
        Assert.Equal(7, Assert.IsType<int>(result.Value));
    }

    [Fact]
    public void EnumEquality_KeepsEnumValue()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Kind == ProductKind.Digital);

        Assert.Equal("Kind", result.PropertyName);
        Assert.Equal(FilterOperator.Equals, result.Operator);
        Assert.Equal((int)ProductKind.Digital, Assert.IsType<int>(result.Value));
    }

    [Fact]
    public void DottedMemberPath_IsPreserved()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Category!.Name == "Tools");

        Assert.Equal("Category.Name", result.PropertyName);
        Assert.Equal(FilterOperator.Equals, result.Operator);
    }

    [Fact]
    public void AndExpression_BecomesAndGroupWithTwoLeaves()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.IsActive && p.Stock > 10);

        Assert.True(result.IsGroup);
        Assert.Equal(FilterLogic.And, result.Logic);
        Assert.Equal(2, result.Filters!.Count);
        Assert.All(result.Filters!, f => Assert.False(f.IsGroup));
    }

    [Fact]
    public void OrExpression_BecomesOrGroupWithTwoLeaves()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.IsActive || p.Stock > 10);

        Assert.True(result.IsGroup);
        Assert.Equal(FilterLogic.Or, result.Logic);
        Assert.Equal(2, result.Filters!.Count);
    }

    [Fact]
    public void NestedSameLogic_IsFlattened()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => p.IsActive && p.Stock > 10 && p.Name == "x");

        Assert.True(result.IsGroup);
        Assert.Equal(FilterLogic.And, result.Logic);
        Assert.Equal(3, result.Filters!.Count);
        Assert.All(result.Filters!, f => Assert.False(f.IsGroup));
    }

    [Fact]
    public void MixedAndOr_KeepsNestedGroup()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(
            p => p.IsActive && (p.Name.Contains("w") || p.Stock < 5));

        Assert.Equal(FilterLogic.And, result.Logic);
        Assert.Equal(2, result.Filters!.Count);

        var nested = Assert.Single(result.Filters!, f => f.IsGroup);
        Assert.Equal(FilterLogic.Or, nested.Logic);
        Assert.Equal(2, nested.Filters!.Count);
    }

    [Fact]
    public void NegatedEquality_FlipsToNotEquals()
    {
        var result = FilterExpressionDecomposer.Decompose<TestProduct>(p => !(p.Stock == 5));

        Assert.False(result.IsGroup);
        Assert.Equal(FilterOperator.NotEquals, result.Operator);
        Assert.Equal(5, Assert.IsType<int>(result.Value));
    }

    [Fact]
    public void NegatedComparison_Throws()
    {
        Assert.Throws<NotSupportedException>(
            () => FilterExpressionDecomposer.Decompose<TestProduct>(p => !(p.Stock > 5)));
    }

    [Fact]
    public void ComparisonWithoutMemberOperand_Throws()
    {
        Assert.Throws<NotSupportedException>(
            () => FilterExpressionDecomposer.Decompose<TestProduct>(p => p.Name.ToUpper() == "X"));
    }
}
