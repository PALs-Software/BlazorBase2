namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// When a <c>BaseList</c> shows tiles instead of its table.
/// </summary>
public enum BaseListMobileLayout
{
    /// <summary>Tiles while the list's container is narrower than the mobile breakpoint, the table otherwise.</summary>
    Auto = 0,

    /// <summary>Always tiles, whatever the width.</summary>
    Always,

    /// <summary>Always the table; the list is not adapted to narrow screens.</summary>
    Never
}
