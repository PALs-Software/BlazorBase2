using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Configuration;

public class BaseListBuilderTests
{
    [Fact]
    public void Column_ResolvesPropertyNameAndDefaults()
    {
        var configuration = new BaseListBuilder<TestProduct>()
            .Column(p => p.Name)
            .Build();

        var column = Assert.Single(configuration.Columns);
        Assert.Equal("Name", column.PropertyName);
        Assert.Null(column.Title);
        Assert.True(column.Sortable);
        Assert.True(column.Visible);
        Assert.Equal(0, column.Order);
    }

    [Fact]
    public void Column_AppliesConfiguredOptions()
    {
        var configuration = new BaseListBuilder<TestProduct>()
            .Column(p => p.Price, c => c.Title("Preis").Format("C2").Sortable(false).Width("120px"))
            .Build();

        var column = Assert.Single(configuration.Columns);
        Assert.Equal("Price", column.PropertyName);
        Assert.Equal("Preis", column.Title);
        Assert.Equal("C2", column.Format);
        Assert.False(column.Sortable);
        Assert.Equal("120px", column.Width);
    }

    [Fact]
    public void MultipleColumns_GetIncrementingOrder()
    {
        var configuration = new BaseListBuilder<TestProduct>()
            .Column(p => p.Name)
            .Column(p => p.Stock)
            .Column(p => p.Price)
            .Build();

        Assert.Equal([0, 1, 2], configuration.Columns.Select(c => c.Order));
        Assert.Equal(["Name", "Stock", "Price"], configuration.Columns.Select(c => c.PropertyName));
    }

    [Fact]
    public void ActionGroup_BuildsGroupWithActions()
    {
        var configuration = new BaseListBuilder<TestProduct>()
            .ActionGroup("Process", group => group
                .Caption("Aktionen")
                .ShowIn(CrudActionContext.ListToolbar)
                .Action("Activate", a => a.ToolTip("Activate the product").BulkAction()))
            .Build();

        var group = Assert.Single(configuration.ActionGroups);
        Assert.Equal("Process", group.Name);
        Assert.Equal("Aktionen", group.Caption);
        Assert.Equal(CrudActionContext.ListToolbar, group.Contexts);

        var action = Assert.Single(group.Actions);
        Assert.Equal("Activate", action.Caption);
        Assert.True(action.IsBulkAction);
        Assert.Equal(Microsoft.FluentUI.AspNetCore.Components.Appearance.Stealth, action.Appearance);
    }
}
