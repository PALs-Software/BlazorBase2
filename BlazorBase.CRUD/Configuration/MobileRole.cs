namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// The part a property plays on the tile a <c>BaseList</c> shows instead of a table row on a narrow screen.
/// </summary>
public enum MobileRole
{
    /// <summary>
    /// Not decided: the list picks the property by its data type, unless the list or the entity declares any
    /// explicit role, in which case an undecided property is left off the tile.
    /// </summary>
    Auto = 0,

    /// <summary>Never shown on the tile.</summary>
    Hidden,

    /// <summary>The headline of the tile. Only the first one counts.</summary>
    Title,

    /// <summary>A secondary line under the title. Only the first one counts.</summary>
    Subtitle,

    /// <summary>A label and value pair.</summary>
    Field,

    /// <summary>A chip: the value of an enum, or the column title when a boolean is true.</summary>
    Badge,

    /// <summary>
    /// The column template is rendered as the tile's action row (buttons such as Accept or Run now) instead of a
    /// label and value pair.
    /// </summary>
    Actions
}
