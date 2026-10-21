using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Validation;

/// <summary>
/// Drop-in replacement for <c>DataAnnotationsValidator</c> inside a card <c>EditForm</c>. It validates the same
/// attributes, but words the default messages in the UI language, labels them with the localized field label and also
/// reports the input errors collected in <see cref="CardFieldErrors"/>.
/// </summary>
public sealed class LocalizedDataAnnotationsValidator : ComponentBase, IDisposable
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ValidatedProperties = new();

    private EditContext? attachedContext;
    private ValidationMessageStore? messageStore;

    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    /// <summary>The framework resource localizer that holds the default message templates.</summary>
    [Parameter, EditorRequired]
    public IStringLocalizer FrameworkLocalizer { get; set; } = default!;

    /// <summary>The model localizer chain used to translate literal <c>ErrorMessage</c> texts.</summary>
    [Parameter]
    public IStringLocalizer? ModelLocalizer { get; set; }

    /// <summary>Resolves the localized label of a property; the property name is used when omitted.</summary>
    [Parameter]
    public Func<string, string>? LabelResolver { get; set; }

    /// <summary>Input errors that must keep the form invalid.</summary>
    [Parameter]
    public CardFieldErrors? FieldErrors { get; set; }

    protected override void OnParametersSet()
    {
        if (CurrentEditContext is null)
            throw new InvalidOperationException($"{nameof(LocalizedDataAnnotationsValidator)} requires a cascading {nameof(EditContext)}.");

        if (ReferenceEquals(attachedContext, CurrentEditContext))
            return;

        Detach();
        Attach(CurrentEditContext);
    }

    private void Attach(EditContext editContext)
    {
        attachedContext = editContext;
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += OnValidationRequested;
        editContext.OnFieldChanged += OnFieldChanged;
    }

    private void Detach()
    {
        if (attachedContext is null)
            return;

        attachedContext.OnValidationRequested -= OnValidationRequested;
        attachedContext.OnFieldChanged -= OnFieldChanged;
        messageStore?.Clear();
        attachedContext.NotifyValidationStateChanged();
        attachedContext = null;
        messageStore = null;
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        if (attachedContext is null || messageStore is null)
            return;

        messageStore.Clear();

        var model = attachedContext.Model;
        var hasMessages = false;

        foreach (var property in GetValidatedProperties(model.GetType()))
            hasMessages |= ValidateProperty(model, property, messageStore);

        if (!hasMessages && model is IValidatableObject validatable)
            AddObjectResults(model, validatable, messageStore);

        attachedContext.NotifyValidationStateChanged();
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        if (attachedContext is null || messageStore is null)
            return;

        var identifier = e.FieldIdentifier;
        if (!ReferenceEquals(identifier.Model, attachedContext.Model) || string.IsNullOrEmpty(identifier.FieldName))
            return;

        var property = GetValidatedProperties(identifier.Model.GetType()).FirstOrDefault(p => p.Name == identifier.FieldName);
        if (property is null)
            return;

        messageStore.Clear(identifier);
        ValidateProperty(identifier.Model, property, messageStore);
        attachedContext.NotifyValidationStateChanged();
    }

    private bool ValidateProperty(object model, PropertyInfo property, ValidationMessageStore store)
    {
        var identifier = new FieldIdentifier(model, property.Name);

        if (FieldErrors?.Get(property.Name) is { } pendingError)
        {
            store.Add(identifier, pendingError);
            return true;
        }

        var label = LabelResolver?.Invoke(property.Name) ?? property.Name;
        var context = new ValidationContext(model) { DisplayName = label, MemberName = property.Name };
        var value = property.GetValue(model);
        var attributes = property.GetCustomAttributes<ValidationAttribute>(inherit: true).OrderBy(a => a is RequiredAttribute ? 0 : 1);

        foreach (var attribute in attributes)
        {
            var result = attribute.GetValidationResult(value, context);
            if (result is null || result == ValidationResult.Success)
                continue;

            var attributeMessage = result.ErrorMessage ?? string.Empty;
            store.Add(identifier, DataAnnotationMessages.Build(attribute, label, attributeMessage, FrameworkLocalizer, ModelLocalizer, LabelResolver));
            return true;
        }

        return false;
    }

    private static void AddObjectResults(object model, IValidatableObject validatable, ValidationMessageStore store)
    {
        foreach (var result in validatable.Validate(new ValidationContext(model)))
        {
            if (result == ValidationResult.Success || result.ErrorMessage is null)
                continue;

            var memberNames = result.MemberNames.Any() ? result.MemberNames : [string.Empty];
            foreach (var memberName in memberNames)
                store.Add(new FieldIdentifier(model, memberName), result.ErrorMessage);
        }
    }

    private PropertyInfo[] GetValidatedProperties(Type modelType) =>
        ValidatedProperties.GetOrAdd(modelType, static type => type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead && p.IsDefined(typeof(ValidationAttribute), inherit: true))
            .ToArray())
        .Concat(PendingErrorProperties(modelType))
        .DistinctBy(p => p.Name)
        .ToArray();

    private IEnumerable<PropertyInfo> PendingErrorProperties(Type modelType)
    {
        if (FieldErrors is null)
            yield break;

        foreach (var propertyName in FieldErrors.All.Keys)
        {
            var property = modelType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is not null)
                yield return property;
        }
    }

    public void Dispose() => Detach();
}
