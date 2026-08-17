using System.Reflection;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Test.Infrastructure.CustomProperties;

/// <summary>
/// Second sample custom input that also handles the "Name" property. Used to verify that the first
/// registered component whose CanHandle returns true wins (registration order).
/// </summary>
public sealed class AlternateNameInput : ComponentBase, IBaseCustomPropertyInput
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
        builder.AddAttribute(1, "class", "alternate-name-input");
        builder.AddAttribute(2, "value", Value as string);
        builder.AddAttribute(3, "readonly", ReadOnly);
        builder.CloseElement();
    }
}
