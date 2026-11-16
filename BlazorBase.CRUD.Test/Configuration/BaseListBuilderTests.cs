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
    public void Column_RecordsTheCellTemplate()
    {
        Microsoft.AspNetCore.Components.RenderFragment<TestProduct> template = product => builder => builder.AddContent(0, product.Name);

        var configuration = new BaseListBuilder<TestProduct>()
            .Column(p => p.Name, c => c.Template(template))
            .Build();

        Assert.Same(template, Assert.Single(configuration.Columns).Template);
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
    public void MobileCard_RecordsTheRolesInDeclarationOrder()
    {
        var configuration = new BaseListBuilder<TestProduct>()
            .Column(p => p.Name)
            .MobileCard(card => card
                .Title(p => p.Name)
                .Subtitle(p => p.Description)
                .Field(p => p.Price, "Preis")
                .Field(p => p.ReleasedOn)
                .Badge(p => p.IsActive)
                .MaxFields(3))
            .MobileBreakpoint(480)
            .Build();

        var card = Assert.IsType<MobileCardConfiguration<TestProduct>>(configuration.MobileCard);
        Assert.Equal(["Name", "Description", "Price", "ReleasedOn", "IsActive"], card.Fields.Select(f => f.PropertyName));
        Assert.Equal(
            [MobileRole.Title, MobileRole.Subtitle, MobileRole.Field, MobileRole.Field, MobileRole.Badge],
            card.Fields.Select(f => f.Role));
        Assert.Equal([0, 1, 2, 3, 4], card.Fields.Select(f => f.Priority));
        Assert.Equal("Preis", card.Fields[2].Label);
        Assert.Equal(3, card.MaxFields);
        Assert.Equal(480, configuration.MobileBreakpoint);
    }

    [Fact]
    public void Mobile_SetsTheRoleAndPriorityOfAColumn()
    {
        var configuration = new BaseListBuilder<TestProduct>()
            .Column(p => p.Name, c => c.Mobile(MobileRole.Title))
            .Column(p => p.Price, c => c.Mobile(MobileRole.Field, 3))
            .Column(p => p.Stock)
            .Build();

        Assert.Equal(MobileRole.Title, configuration.Columns[0].MobileRole);
        Assert.Equal(MobileRole.Field, configuration.Columns[1].MobileRole);
        Assert.Equal(3, configuration.Columns[1].MobilePriority);
        Assert.Equal(MobileRole.Auto, configuration.Columns[2].MobileRole);
        Assert.Null(configuration.Columns[2].MobilePriority);
        Assert.Null(configuration.MobileCard);
        Assert.Null(configuration.MobileBreakpoint);
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
        Assert.Equal(Microsoft.FluentUI.AspNetCore.Components.ButtonAppearance.Transparent, action.Appearance);
    }
}
