using System.Reflection;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Test.Infrastructure.CustomProperties;

/// <summary>
/// Sample custom input fixture that handles the string property named "Name". Renders a marked
/// input and round-trips its value through <see cref="ValueChanged"/>.
/// </summary>
public sealed class SampleNameInput : ComponentBase, IBaseCustomPropertyInput
{
    [Parameter]
    public object Model { get; set; } = default!;

    [Parameter]
    public PropertyInfo Property { get; set; } = default!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    [Parameter]
    public bool IsEditing { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    public bool CanHandle(CustomPropertyContext context) =>
        context.PropertyType == typeof(string) && context.Property.Name == nameof(TestProduct.Name);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "class", "sample-name-input");
        builder.AddAttribute(2, "value", Value as string);
        builder.AddAttribute(3, "readonly", ReadOnly);
        builder.AddAttribute(4, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, OnInputAsync));
        builder.CloseElement();
    }

    private async Task OnInputAsync(ChangeEventArgs args)
    {
        await ValueChanged.InvokeAsync(args.Value);
    }
}
