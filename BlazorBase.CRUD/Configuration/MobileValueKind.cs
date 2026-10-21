namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// The kind of value a column holds, as far as the tile of a list cares about it.
/// </summary>
public enum MobileValueKind
{
    /// <summary>A string.</summary>
    Text,

    /// <summary>A date or a date and time.</summary>
    Date,

    /// <summary>A whole or decimal number.</summary>
    Number,

    /// <summary>A boolean.</summary>
    Flag,

    /// <summary>An enum.</summary>
    Enum,

    /// <summary>Anything else that a template or a formatter makes readable.</summary>
    Other,

    /// <summary>Never offered automatically: raw identifiers, binary data, collections and untemplated objects.</summary>
    Excluded
}
