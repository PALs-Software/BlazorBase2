namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Fluent builder for <see cref="ListFilterDefaults"/>.
/// </summary>
/// <example>
/// <code>
/// new ListFilterDefaultsBuilder&lt;Order&gt;()
///     .Preset("pending", "Only pending", filter => filter.Is(order => order.Status, OrderStatus.Pending), isDefault: true)
///     .Preset("active", "Only active", filter => filter.Is(order => order.IsActive, true))
///     .Build();
/// </code>
/// </example>
public sealed class ListFilterDefaultsBuilder<TModel> where TModel : class
{
    private readonly ListFilterDefaults defaults = new();

    /// <summary>
    /// True once a key, preset or default filter has been declared.
    /// </summary>
    public bool HasContent => defaults.ListKey is not null || defaults.Presets.Count > 0 || defaults.DefaultFilter is not null;

    /// <summary>
    /// Sets the key saved filters are stored under, instead of the model type's full name.
    /// </summary>
    public ListFilterDefaultsBuilder<TModel> ListKey(string listKey)
    {
        defaults.ListKey = ListFilterValidator.ValidateListKey(listKey);
        return this;
    }

    /// <summary>
    /// Offers a named filter to every user. <paramref name="id"/> must be unique within the list.
    /// </summary>
    /// <param name="id">Stable identifier; also the localization key suffix <c>FilterPreset_{id}</c>.</param>
    /// <param name="name">Display name used when no localized text exists.</param>
    /// <param name="configure">Declares the conditions.</param>
    /// <param name="isDefault">Applies the preset on first load unless the user saved a default of their own.</param>
    public ListFilterDefaultsBuilder<TModel> Preset(string id, string name, Action<FilterGroupBuilder<TModel>> configure, bool isDefault = false)
    {
        if (String.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A preset needs an id.", nameof(id));

        if (defaults.Presets.Any(preset => String.Equals(preset.Id, id, StringComparison.Ordinal)))
            throw new InvalidOperationException($"A filter preset with the id '{id}' is already declared.");

        if (isDefault && defaults.Presets.Any(preset => preset.IsDefault))
            throw new InvalidOperationException("Only one filter preset can be the default.");

        var builder = new FilterGroupBuilder<TModel>();
        configure(builder);

        defaults.Presets.Add(new FilterPreset
        {
            Id = id,
            Name = name,
            Filter = builder.Build(),
            IsDefault = isDefault
        });
        return this;
    }

    /// <summary>
    /// Declares an unnamed filter applied on first load unless the user saved a default of their own. Not offered as a
    /// preset; use <see cref="Preset"/> with <c>isDefault</c> when users should be able to reload it.
    /// </summary>
    public ListFilterDefaultsBuilder<TModel> DefaultFilter(Action<FilterGroupBuilder<TModel>> configure)
    {
        var builder = new FilterGroupBuilder<TModel>();
        configure(builder);
        defaults.DefaultFilter = builder.Build();
        return this;
    }

    public ListFilterDefaults Build() => defaults;
}
