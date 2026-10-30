using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.SavedFilters;
using Xunit;

namespace BlazorBase.CRUD.Test.SavedFilters;

public class ListFilterSerializerAndValidatorTests
{
    private static FilterGroupModel Sample() => new()
    {
        Logic = FilterLogic.Or,
        Conditions = [new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Contains, Value = "abc" }],
        Groups = [new FilterGroupModel { Conditions = [new FilterConditionModel { PropertyName = "Stock", Operator = FilterOperator.IsNull }] }]
    };

    [Fact]
    public void Serialize_WritesEnumsByName_AndRoundTrips()
    {
        var json = ListFilterSerializer.Serialize(Sample());
        var restored = ListFilterSerializer.Deserialize(json);

        Assert.Contains("\"Or\"", json);
        Assert.Contains("\"Contains\"", json);
        Assert.Equal(FilterLogic.Or, restored.Logic);
        Assert.Equal("abc", restored.Conditions[0].Value);
        Assert.Equal(FilterOperator.IsNull, restored.Groups[0].Conditions[0].Operator);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void Deserialize_UnreadableInput_YieldsEmptyFilter(string? json)
    {
        var restored = ListFilterSerializer.Deserialize(json);

        Assert.Empty(restored.Conditions);
        Assert.Empty(restored.Groups);
    }

    [Fact]
    public void Canonicalize_IgnoresIncompleteConditions()
    {
        var complete = Sample();
        var withNoise = Sample();
        withNoise.Conditions.Add(new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Equals, Value = " " });

        Assert.Equal(ListFilterSerializer.Canonicalize(complete), ListFilterSerializer.Canonicalize(withNoise));
    }

    [Fact]
    public void NormalizeName_TrimsAndRejectsEmptyOrTooLong()
    {
        Assert.Equal("Pending", ListFilterValidator.NormalizeName("  Pending "));
        Assert.Throws<ListFilterValidationException>(() => ListFilterValidator.NormalizeName("   "));
        Assert.Throws<ListFilterValidationException>(() => ListFilterValidator.NormalizeName(new string('x', ListFilterValidator.MaxNameLength + 1)));
    }

    [Fact]
    public void ValidateFilter_RejectsTooDeepNesting()
    {
        var root = new FilterGroupModel();
        var current = root;

        for (var level = 0; level < ListFilterValidator.MaxDepth; level++)
        {
            var child = new FilterGroupModel();
            current.Groups.Add(child);
            current = child;
        }

        Assert.Throws<ListFilterValidationException>(() => ListFilterValidator.ValidateFilter(root));
    }

    [Fact]
    public void ValidateFilter_RejectsTooManyConditionsAndLongValuesAndUnknownOperators()
    {
        var many = new FilterGroupModel
        {
            Conditions = [.. Enumerable.Range(0, ListFilterValidator.MaxConditions + 1).Select(_ => new FilterConditionModel { PropertyName = "Name", Value = "x" })]
        };
        var longValue = new FilterGroupModel
        {
            Conditions = [new FilterConditionModel { PropertyName = "Name", Value = new string('x', ListFilterValidator.MaxValueLength + 1) }]
        };
        var unknownOperator = new FilterGroupModel
        {
            Conditions = [new FilterConditionModel { PropertyName = "Name", Operator = (FilterOperator)999, Value = "x" }]
        };

        Assert.Throws<ListFilterValidationException>(() => ListFilterValidator.ValidateFilter(many));
        Assert.Throws<ListFilterValidationException>(() => ListFilterValidator.ValidateFilter(longValue));
        Assert.Throws<ListFilterValidationException>(() => ListFilterValidator.ValidateFilter(unknownOperator));
        Assert.Throws<ListFilterValidationException>(() => ListFilterValidator.ValidateFilter(null));
        ListFilterValidator.ValidateFilter(Sample());
    }
}
