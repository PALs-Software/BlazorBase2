namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// What the tile of a list shows, resolved from the list columns, the tile configuration and the entity
/// attributes by <see cref="MobileCardPlanResolver"/>.
/// </summary>
public sealed class MobileCardPlan<TModel>
{
    /// <summary>The headline column; null when the list has no column to take it from.</summary>
    public PropertyColumnConfig<TModel>? Title { get; init; }

    public PropertyColumnConfig<TModel>? Subtitle { get; init; }

    /// <summary>The label and value pairs, in display order.</summary>
    public IReadOnlyList<PropertyColumnConfig<TModel>> Fields { get; init; } = [];

    /// <summary>The columns shown as chips, in display order.</summary>
    public IReadOnlyList<PropertyColumnConfig<TModel>> Badges { get; init; } = [];

    /// <summary>The columns whose template is the tile's action row.</summary>
    public IReadOnlyList<PropertyColumnConfig<TModel>> ActionColumns { get; init; } = [];

    /// <summary>Labels declared in the tile configuration, taking precedence over the column title.</summary>
    public IReadOnlyDictionary<PropertyColumnConfig<TModel>, string> Labels { get; init; } = new Dictionary<PropertyColumnConfig<TModel>, string>();

    /// <summary>Columns created for properties the tile configuration names but the list has no column for.</summary>
    public IReadOnlyList<PropertyColumnConfig<TModel>> ExtraColumns { get; init; } = [];

    /// <summary>Every column the tile reads a value from.</summary>
    public IEnumerable<PropertyColumnConfig<TModel>> DisplayedColumns
    {
        get
        {
            if (Title is not null)
                yield return Title;

            if (Subtitle is not null)
                yield return Subtitle;

            foreach (var column in Badges)
                yield return column;

            foreach (var column in Fields)
                yield return column;
        }
    }

    /// <summary>The label of a field: the one declared for the tile, otherwise null.</summary>
    public string? LabelFor(PropertyColumnConfig<TModel> column) => Labels.TryGetValue(column, out var label) ? label : null;
}
