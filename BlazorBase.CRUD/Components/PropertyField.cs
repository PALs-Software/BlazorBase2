using System.Linq.Expressions;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Marker child component for declaring fields inside a BaseCard or BaseListPart.
/// Collected by the parent component to build field configuration.
/// </summary>
public class PropertyField<TModel> : ComponentBase
{
    [Parameter, EditorRequired]
    public Expression<Func<TModel, object?>> Property { get; set; } = default!;

    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// Optional field tooltip. When omitted, resolved from the model localizer key
    /// <c>&lt;PropertyName&gt;_Tooltip</c>.
    /// </summary>
    [Parameter]
    public string? Tooltip { get; set; }

    [Parameter]
    public bool Editable { get; set; } = true;

    [Parameter]
    public bool Required { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public string? Group { get; set; }

    [Parameter]
    public int Order { get; set; }

    [Parameter]
    public int ColSpan { get; set; } = 1;

    [Parameter]
    public int? Lines { get; set; }

    [Parameter]
    public RenderFragment<Configuration.PropertyFieldContext<TModel>>? EditorTemplate { get; set; }

    [Parameter]
    public RenderFragment<Configuration.PropertyFieldContext<TModel>>? DisplayTemplate { get; set; }

    /// <summary>
    /// Marks this field's property as a display key contributing to the card header
    /// title, ordered by <see cref="DisplayKeyOrder"/>.
    /// </summary>
    [Parameter]
    public bool IsDisplayKey { get; set; }

    /// <summary>Position within the display-key title; lower values appear first.</summary>
    [Parameter]
    public int DisplayKeyOrder { get; set; }

    /// <summary>
    /// Comma-separated role list for a single view-level access rule (e.g. "Admin, User" or "*").
    /// Combined with <see cref="AccessRights"/> to form one <see cref="CrudAccessRule"/>.
    /// For multiple rules use the fluent builder API.
    /// </summary>
    [Parameter]
    public string? AccessRoles { get; set; }

    /// <summary>
    /// Rights string (R/I/M/D, e.g. "RM" or "RIMD") for the single view-level access rule.
    /// </summary>
    [Parameter]
    public string? AccessRights { get; set; }

    [CascadingParameter]
    private IFieldCollector<TModel>? FieldCollector { get; set; }

    [CascadingParameter]
    private IDisplayKeyCollector<TModel>? DisplayKeyCollector { get; set; }

    protected override void OnInitialized()
    {
        FieldCollector?.AddField(ToConfig());

        if (!IsDisplayKey || DisplayKeyCollector is null)
            return;

        var displayKey = new Configuration.DisplayKeyFieldConfig<TModel> { Property = Property, Order = DisplayKeyOrder };
        displayKey.ResolvePropertyName();
        DisplayKeyCollector.AddDisplayKey(displayKey);
    }

    internal Configuration.PropertyFieldConfig<TModel> ToConfig()
    {
        var config = new Configuration.PropertyFieldConfig<TModel>
        {
            Property = Property,
            Label = Label,
            Tooltip = Tooltip,
            Editable = Editable,
            Required = Required,
            Placeholder = Placeholder,
            Group = Group,
            Order = Order,
            ColSpan = ColSpan,
            Lines = Lines,
            EditorTemplate = EditorTemplate,
            DisplayTemplate = DisplayTemplate
        };
        config.ResolvePropertyName();

        if (!string.IsNullOrWhiteSpace(AccessRoles) && AccessRights is not null)
            config.AccessRules.Add(CrudAccessResolver.ParseRule(AccessRoles, AccessRights, allowDelete: true));

        return config;
    }
}

internal interface IFieldCollector<TModel>
{
    void AddField(Configuration.PropertyFieldConfig<TModel> field);

    void RegisterSaveParticipant(ICardSaveParticipant participant);

    void UnregisterSaveParticipant(ICardSaveParticipant participant);

    void NotifyNavigationFieldLoaded();
}
