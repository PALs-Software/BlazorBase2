using System.Reflection;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Test.Infrastructure.CustomProperties;

/// <summary>
/// Sample custom input that handles the "Description" property and participates in the card save
/// lifecycle. All lifecycle calls are forwarded to the injected <see cref="SaveParticipantRecorder"/>
/// so tests can assert ordering and blocking behavior.
/// </summary>
public sealed class SaveParticipantInput : ComponentBase, IBaseCustomPropertyInput, ICardSaveParticipant
{
    #region Injects

    [Inject]
    private SaveParticipantRecorder Recorder { get; set; } = default!;

    #endregion

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
        context.PropertyType == typeof(string) && context.Property.Name == nameof(TestProduct.Description);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "class", "save-participant-input");
        builder.AddAttribute(2, "value", Value as string);
        builder.AddAttribute(3, "readonly", ReadOnly);
        builder.CloseElement();
    }

    public Task<bool> ValidateAsync()
    {
        Recorder.Calls.Add(nameof(ValidateAsync));
        return Task.FromResult(Recorder.ValidationResult);
    }

    public Task OnBeforeSaveAsync(CardSaveContext context)
    {
        Recorder.Calls.Add(nameof(OnBeforeSaveAsync));
        Recorder.BeforeSaveContext = context;
        return Task.CompletedTask;
    }

    public Task OnAfterSaveAsync(CardSaveContext context)
    {
        Recorder.Calls.Add(nameof(OnAfterSaveAsync));
        Recorder.AfterSaveContext = context;
        return Task.CompletedTask;
    }
}
