using System.Linq.Expressions;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Marker child component for declaring columns inside a BaseList.
/// Collected by the parent BaseList to build column configuration.
/// </summary>
public class BaseColumn<TModel> : ComponentBase
{
    [Parameter, EditorRequired]
    public Expression<Func<TModel, object?>> Property { get; set; } = default!;

    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// Optional header tooltip. When omitted, resolved from the model localizer key
    /// <c>&lt;PropertyName&gt;_Tooltip</c>.
    /// </summary>
    [Parameter]
    public string? Tooltip { get; set; }

    [Parameter]
    public bool Sortable { get; set; } = true;

    [Parameter]
    public bool Visible { get; set; } = true;

    [Parameter]
    public RenderFragment<TModel>? Template { get; set; }

    [Parameter]
    public string? Format { get; set; }

    [Parameter]
    public int Order { get; set; }

    [Parameter]
    public string? Width { get; set; }

    /// <summary>
    /// Comma-separated role list for a single view-level access rule (e.g. "Admin, User" or "*").
    /// Combined with <see cref="AccessRights"/> to form one <see cref="CrudAccessRule"/>.
    /// </summary>
    [Parameter]
    public string? AccessRoles { get; set; }

    /// <summary>
    /// Rights string (R/I/M/D, e.g. "R") for the single view-level access rule.
    /// </summary>
    [Parameter]
    public string? AccessRights { get; set; }

    [CascadingParameter]
    private IColumnCollector<TModel>? ColumnCollector { get; set; }

    protected override void OnInitialized()
    {
        ColumnCollector?.AddColumn(ToConfig());
    }

    internal Configuration.PropertyColumnConfig<TModel> ToConfig()
    {
        var config = new Configuration.PropertyColumnConfig<TModel>
        {
            Property = Property,
            Title = Title,
            Tooltip = Tooltip,
            Sortable = Sortable,
            Visible = Visible,
            Template = Template,
            Format = Format,
            Order = Order,
            Width = Width
        };
        config.ResolvePropertyName();

        if (!string.IsNullOrWhiteSpace(AccessRoles) && AccessRights is not null)
            config.AccessRules.Add(CrudAccessResolver.ParseRule(AccessRoles, AccessRights, allowDelete: true));

        return config;
    }
}

internal interface IColumnCollector<TModel>
{
    void AddColumn(Configuration.PropertyColumnConfig<TModel> column);
}
