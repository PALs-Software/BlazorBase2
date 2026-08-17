using System.Linq.Expressions;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

[Collection(CustomPropertyResolutionCollection.Name)]
public class BasePropertyInputTests : BunitTestContextBase
{
    [Fact]
    public void BoolProperty_RendersSwitch()
    {
        var cut = RenderInput(new TestProduct { IsActive = true }, Field(p => p.IsActive));

        Assert.Single(cut.FindComponents<FluentSwitch>());
    }

    [Fact]
    public void DateTimeProperty_RendersDatePicker()
    {
        var cut = RenderInput(new TestProduct { ReleasedOn = new DateTime(2026, 1, 1) }, Field(p => p.ReleasedOn));

        Assert.Single(cut.FindComponents<FluentDatePicker>());
    }

    [Fact]
    public void NumericProperty_RendersNumericTextField()
    {
        var cut = RenderInput(new TestProduct { Stock = 5 }, Field(p => p.Stock));

        var textField = cut.FindComponent<FluentTextField>();
        Assert.Equal(InputMode.Numeric, textField.Instance.InputMode);
    }

    [Fact]
    public void StringProperty_RendersPlainTextField()
    {
        var cut = RenderInput(new TestProduct { Name = "P" }, Field(p => p.Name));

        Assert.Single(cut.FindComponents<FluentTextField>());
        Assert.Empty(cut.FindComponents<FluentTextArea>());
    }

    [Fact]
    public void MultilineProperty_RendersTextAreaWithRowCount()
    {
        var cut = RenderInput(new TestProduct { Description = "text" }, Field(p => p.Description, f => f.Lines(3)));

        var textArea = cut.FindComponent<FluentTextArea>();
        Assert.Equal(3, textArea.Instance.Rows);
    }

    [Fact]
    public void EnumProperty_RendersSelectWithOneOptionPerValue()
    {
        var cut = RenderInput(new TestProduct { Kind = ProductKind.Digital }, Field(p => p.Kind));

        Assert.Single(cut.FindComponents<FluentSelect<string>>());
        Assert.Equal(Enum.GetValues<ProductKind>().Length, cut.FindComponents<FluentOption<string>>().Count);
    }

    [Fact]
    public void NavigationProperty_WithRegisteredProvider_RendersSelectWithLookupOptions()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("test-user");
        authorization.SetRoles("User");

        var categories = new[]
        {
            new TestCategory { Id = Guid.NewGuid(), Name = "Cat A" },
            new TestCategory { Id = Guid.NewGuid(), Name = "Cat B" }
        };

        var categoryProvider = Substitute.For<IBaseDataProvider<TestCategory>>();
        categoryProvider.GetCountAsync(Arg.Any<Expression<Func<TestCategory, bool>>>(), Arg.Any<CancellationToken>()).Returns(categories.Length);
        categoryProvider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestCategory, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestCategory> { Items = [.. categories], TotalCount = categories.Length });
        Services.AddSingleton(categoryProvider);

        var cut = RenderInput(new TestProduct(), Field(p => p.Category));

        Assert.Single(cut.FindComponents<FluentSelect<string>>());
        Assert.Contains("Cat A", cut.Markup);
        Assert.Contains("Cat B", cut.Markup);
    }

    private IRenderedComponent<BasePropertyInput<TestProduct>> RenderInput(
        TestProduct model,
        PropertyFieldConfig<TestProduct> field,
        bool editing = true)
    {
        return Render<BasePropertyInput<TestProduct>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.FieldConfig, field)
            .Add(p => p.IsEditing, editing));
    }

    private static PropertyFieldConfig<TestProduct> Field(
        Expression<Func<TestProduct, object?>> property,
        Action<FieldBuilder<TestProduct>>? configure = null)
    {
        return new BaseCardBuilder<TestProduct>().Field(property, configure).Build().Fields[0];
    }
}
