using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Test.Infrastructure.CustomProperties;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Configuration;

public class BaseCardBuilderTests
{
    [Fact]
    public void Field_ResolvesPropertyNameAndDefaults()
    {
        var configuration = new BaseCardBuilder<TestProduct>()
            .Field(p => p.Name)
            .Build();

        var field = Assert.Single(configuration.Fields);
        Assert.Equal("Name", field.PropertyName);
        Assert.True(field.Editable);
        Assert.False(field.Required);
        Assert.Equal(1, field.ColSpan);
        Assert.Null(field.Group);
        Assert.Equal(20, field.LookupThreshold);
    }

    [Fact]
    public void Field_AppliesConfiguredOptions()
    {
        var configuration = new BaseCardBuilder<TestProduct>()
            .Field(p => p.Description, f => f.Label("Beschreibung").Required().Lines(4).ColSpan(2).Placeholder("..."))
            .Build();

        var field = Assert.Single(configuration.Fields);
        Assert.Equal("Beschreibung", field.Label);
        Assert.True(field.Required);
        Assert.Equal(4, field.Lines);
        Assert.Equal(2, field.ColSpan);
        Assert.Equal("...", field.Placeholder);
    }

    [Fact]
    public void Group_AssignsGroupNameToContainedFields()
    {
        var configuration = new BaseCardBuilder<TestProduct>()
            .Field(p => p.Name)
            .Group("Details", g => g
                .Field(p => p.Price)
                .Field(p => p.Stock))
            .Build();

        Assert.Null(configuration.Fields.Single(f => f.PropertyName == "Name").Group);
        Assert.Equal("Details", configuration.Fields.Single(f => f.PropertyName == "Price").Group);
        Assert.Equal("Details", configuration.Fields.Single(f => f.PropertyName == "Stock").Group);
    }

    [Fact]
    public void Fields_GetIncrementingOrderAcrossGroups()
    {
        var configuration = new BaseCardBuilder<TestProduct>()
            .Field(p => p.Name)
            .Group("Details", g => g.Field(p => p.Price))
            .Build();

        Assert.Equal(0, configuration.Fields.Single(f => f.PropertyName == "Name").Order);
        Assert.Equal(1, configuration.Fields.Single(f => f.PropertyName == "Price").Order);
    }

    [Fact]
    public void ListPart_AddsConfiguredListPart()
    {
        var configuration = new BaseCardBuilder<TestProduct>()
            .ListPart<TestReview>(p => p.Reviews, lp => lp.AllowAdd().AllowDelete().AllowReorder())
            .Build();

        var listPart = Assert.Single(configuration.ListParts);
        Assert.Equal("Reviews", listPart.PropertyName);
        Assert.True(listPart.AllowAdd);
        Assert.True(listPart.AllowDelete);
        Assert.True(listPart.AllowReorder);
    }

    [Fact]
    public void ListPart_ChildCardType_SetsPerItemCardComponent()
    {
        var configuration = new BaseCardBuilder<TestProduct>()
            .ListPart<TestReview>(p => p.Reviews, lp => lp.ChildCardType<SampleNameInput>())
            .Build();

        var listPart = Assert.Single(configuration.ListParts);
        Assert.Equal(typeof(SampleNameInput), listPart.ChildCardType);
        Assert.Null(listPart.ListPartComponentType);
    }

    [Fact]
    public void Field_Access_AddsViewLevelRule()
    {
        var configuration = new BaseCardBuilder<TestProduct>()
            .Field(p => p.CostPrice, f => f.Access("Admin", "RM"))
            .Build();

        var field = Assert.Single(configuration.Fields);
        var rule = Assert.Single(field.AccessRules);
        Assert.Equal(["Admin"], rule.Roles);
    }
}
