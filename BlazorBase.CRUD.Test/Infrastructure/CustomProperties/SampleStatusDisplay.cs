using System.Reflection;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Test.Infrastructure.CustomProperties;

/// <summary>
/// Sample custom list-cell display fixture that handles the boolean "IsActive" property and renders
/// a marked status badge.
/// </summary>
public sealed class SampleStatusDisplay : ComponentBase, IBaseCustomPropertyDisplay
{
    [Parameter]
    public object Model { get; set; } = default!;

    [Parameter]
    public PropertyInfo Property { get; set; } = default!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    public bool CanHandle(CustomPropertyContext context) =>
        context.PropertyType == typeof(bool) && context.Property.Name == nameof(TestProduct.IsActive);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "sample-status-badge");
        builder.AddContent(2, Value is true ? "ON" : "OFF");
        builder.CloseElement();
    }
}
