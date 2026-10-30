using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.SavedFilters;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.SavedFilters;

public class ListFilterDefaultsBuilderTests
{
    [Fact]
    public void Preset_StoresConditionsAsTheDrawerWritesThem()
    {
        var defaults = new ListFilterDefaultsBuilder<TestProduct>()
            .Preset("active", "Only active", filter => filter
                .Is(product => product.IsActive, true)
                .Is(product => product.Kind, ProductKind.Digital)
                .Where(product => product.Name, FilterOperator.Contains, "pro")
                .Where(product => product.PreferredKind, FilterOperator.IsNull, "ignored")
                .Where(product => product.ReleasedOn, FilterOperator.GreaterThan, new DateTime(2025, 3, 4, 12, 0, 0)))
            .Build();

        var conditions = defaults.Presets[0].Filter.Conditions;

        Assert.Equal(["true", "Digital", "pro", null, "2025-03-04"], conditions.Select(c => c.Value));
        Assert.Equal(["IsActive", "Kind", "Name", "PreferredKind", "ReleasedOn"], conditions.Select(c => c.PropertyName));
    }

    [Fact]
    public void Preset_RejectsDuplicateIdsAndSecondDefault()
    {
        var builder = new ListFilterDefaultsBuilder<TestProduct>()
            .Preset("a", "A", filter => filter.Is(product => product.IsActive, true), isDefault: true);

        Assert.Throws<InvalidOperationException>(() => builder.Preset("a", "Again", filter => { }));
        Assert.Throws<InvalidOperationException>(() => builder.Preset("b", "B", filter => { }, isDefault: true));
        Assert.Throws<ArgumentException>(() => builder.Preset(" ", "Blank", filter => { }));
    }

    [Fact]
    public void ResolveCodeDefault_PrefersTheDefaultPresetOverTheUnnamedDefaultFilter()
    {
        var onlyUnnamed = new ListFilterDefaultsBuilder<TestProduct>()
            .DefaultFilter(filter => filter.Is(product => product.Stock, 1))
            .Build();
        var both = new ListFilterDefaultsBuilder<TestProduct>()
            .DefaultFilter(filter => filter.Is(product => product.Stock, 1))
            .Preset("p", "P", filter => filter.Is(product => product.Stock, 2), isDefault: true)
            .Build();

        Assert.Equal("1", onlyUnnamed.ResolveCodeDefault()!.Conditions[0].Value);
        Assert.Equal("2", both.ResolveCodeDefault()!.Conditions[0].Value);
        Assert.Null(new ListFilterDefaults().ResolveCodeDefault());
    }

    [Fact]
    public void Group_AndOr_BuildNestedTrees()
    {
        var defaults = new ListFilterDefaultsBuilder<TestProduct>()
            .DefaultFilter(filter => filter
                .Is(product => product.IsActive, true)
                .Group(group => group.Or().Is(product => product.Stock, 1).Is(product => product.Stock, 2)))
            .Build();

        Assert.Equal(FilterLogic.Or, defaults.DefaultFilter!.Groups[0].Logic);
        Assert.Equal(2, defaults.DefaultFilter.Groups[0].Conditions.Count);
    }

    [Fact]
    public void BaseListBuilder_CarriesDefaultsIntoTheConfiguration_OnlyWhenDeclared()
    {
        var plain = new BaseListBuilder<TestProduct>().Column(product => product.Name).Build();
        var configured = new BaseListBuilder<TestProduct>()
            .Column(product => product.Name)
            .FilterListKey("products-admin")
            .FilterPreset("active", "Active", filter => filter.Is(product => product.IsActive, true))
            .DefaultFilter(filter => filter.Is(product => product.Stock, 0))
            .Build();

        Assert.Null(plain.FilterDefaults);
        Assert.Equal("products-admin", configured.FilterDefaults!.ListKey);
        Assert.Single(configured.FilterDefaults.Presets);
        Assert.NotNull(configured.FilterDefaults.DefaultFilter);
    }

    [Fact]
    public void Pruner_DropsUnknownFieldsDisallowedOperatorsAndIncompleteConditions()
    {
        FilterFieldMetadata[] fields =
        [
            new("Name", "Name", FilterFieldKind.Text, typeof(string), false, [FilterOperator.Equals, FilterOperator.Contains], null)
        ];
        var filter = new FilterGroupModel
        {
            Conditions =
            [
                new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Equals, Value = "ok" },
                new FilterConditionModel { PropertyName = "Gone", Operator = FilterOperator.Equals, Value = "x" },
                new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.StartsWith, Value = "x" },
                new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Equals, Value = "" }
            ],
            Groups = [new FilterGroupModel { Conditions = [new FilterConditionModel { PropertyName = "Gone", Value = "x" }] }]
        };

        var pruned = FilterModelPruner.Prune(filter, fields);

        Assert.Single(pruned.Conditions);
        Assert.Empty(pruned.Groups);
    }
}
