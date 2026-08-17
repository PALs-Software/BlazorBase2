using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using Xunit;

namespace BlazorBase.CRUD.Test.Filtering;

public class FilterModelConverterTests
{
    [Fact]
    public void ToDescriptor_NestedGroups_DropsIncompleteConditions()
    {
        var root = new FilterGroupModel
        {
            Logic = FilterLogic.Or,
            Conditions =
            [
                new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Contains, Value = "foo" },
                new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Contains, Value = "  " },
                new FilterConditionModel { PropertyName = null, Operator = FilterOperator.Equals, Value = "x" }
            ],
            Groups =
            [
                new FilterGroupModel
                {
                    Logic = FilterLogic.And,
                    Conditions =
                    [
                        new FilterConditionModel { PropertyName = "Price", Operator = FilterOperator.GreaterThan, Value = "10" },
                        new FilterConditionModel { PropertyName = "Stock", Operator = FilterOperator.IsNull }
                    ]
                }
            ]
        };

        var descriptor = FilterModelConverter.ToDescriptor(root);

        Assert.NotNull(descriptor);
        Assert.Equal(FilterLogic.Or, descriptor!.Logic);
        Assert.Equal(2, descriptor.Filters!.Count);

        var leaf = descriptor.Filters[0];
        Assert.Equal("Name", leaf.PropertyName);
        Assert.Equal("foo", leaf.Value);

        var subgroup = descriptor.Filters[1];
        Assert.True(subgroup.IsGroup);
        Assert.Equal(2, subgroup.Filters!.Count);
        Assert.Null(subgroup.Filters[1].Value);
    }

    [Fact]
    public void ToDescriptor_AllIncomplete_ReturnsNull()
    {
        var root = new FilterGroupModel
        {
            Conditions = [new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Contains, Value = null }]
        };

        Assert.Null(FilterModelConverter.ToDescriptor(root));
    }

    [Fact]
    public void CountConditions_CountsCompleteOnly()
    {
        var root = new FilterGroupModel
        {
            Conditions =
            [
                new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Contains, Value = "a" },
                new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Contains, Value = null }
            ],
            Groups =
            [
                new FilterGroupModel
                {
                    Conditions = [new FilterConditionModel { PropertyName = "Stock", Operator = FilterOperator.IsNotNull }]
                }
            ]
        };

        Assert.Equal(2, FilterModelConverter.CountConditions(root));
    }
}
