using System.Linq.Expressions;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Extensions;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.CustomProperties;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

[Collection(CustomPropertyResolutionCollection.Name)]
public class CustomPropertyInputTests : BunitTestContextBase
{
    [Fact]
    public void RegisteredCustomInput_RendersWhenCanHandleMatches()
    {
        Services.AddBlazorBaseCustomInput<SampleNameInput>();

        var cut = RenderInput(new TestProduct { Name = "P" }, Field(p => p.Name));

        Assert.Single(cut.FindComponents<SampleNameInput>());
        Assert.Empty(cut.FindComponents<FluentTextField>());
    }

    [Fact]
    public void RegisteredCustomInput_NotUsedForNonMatchingProperty()
    {
        Services.AddBlazorBaseCustomInput<SampleNameInput>();

        var cut = RenderInput(new TestProduct { Description = "D" }, Field(p => p.Description));

        Assert.Empty(cut.FindComponents<SampleNameInput>());
        Assert.Single(cut.FindComponents<FluentTextField>());
    }

    [Fact]
    public void RegisteredCustomInput_RoundTripsValueThroughValueChanged()
    {
        Services.AddBlazorBaseCustomInput<SampleNameInput>();

        var model = new TestProduct { Name = "before" };
        var cut = RenderInput(model, Field(p => p.Name));

        cut.Find("input.sample-name-input").Input("after");

        Assert.Equal("after", model.Name);
    }

    [Fact]
    public void MultipleCustomInputs_FirstRegisteredMatchWins()
    {
        Services.AddBlazorBaseCustomInput<SampleNameInput>();
        Services.AddBlazorBaseCustomInput<AlternateNameInput>();

        var cut = RenderInput(new TestProduct { Name = "P" }, Field(p => p.Name));

        Assert.Single(cut.FindComponents<SampleNameInput>());
        Assert.Empty(cut.FindComponents<AlternateNameInput>());
    }

    [Fact]
    public void EditorTemplate_StillWinsOverRegisteredCustomInput()
    {
        Services.AddBlazorBaseCustomInput<SampleNameInput>();

        var field = Field(p => p.Name, f => f.EditorTemplate(BuildMarkerTemplate()));

        var cut = RenderInput(new TestProduct { Name = "P" }, field);

        Assert.Empty(cut.FindComponents<SampleNameInput>());
        Assert.Contains("editor-template-marker", cut.Markup);
    }

    private static RenderFragment<PropertyFieldContext<TestProduct>> BuildMarkerTemplate()
    {
        return _ => (RenderTreeBuilder builder) =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "editor-template-marker");
            builder.CloseElement();
        };
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
