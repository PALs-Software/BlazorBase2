namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Controls which scalar entity fields a <see cref="FilterConfiguration{TModel}"/> exposes.
/// </summary>
public enum FilterFieldSelectionMode
{
    /// <summary>All scalar fields are filterable; configured fields only override label/operators.</summary>
    Auto,

    /// <summary>Only the explicitly configured fields are filterable.</summary>
    Include,

    /// <summary>All scalar fields except the configured ones are filterable.</summary>
    Exclude
}
