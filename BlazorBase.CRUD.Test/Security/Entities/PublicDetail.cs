using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Security.Entities;

/// <summary>
/// Readable reference-navigation target carrying one Admin-restricted scalar,
/// used to verify nested field stripping through a readable navigation.
/// </summary>
public class PublicDetail
{
    public int Id { get; set; }

    public string? Info { get; set; }

    [CrudAccess("Admin", "RIM")]
    public string? RestrictedInfo { get; set; }
}
