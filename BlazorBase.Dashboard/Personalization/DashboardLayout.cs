using System.Text.Json.Serialization;

namespace BlazorBase.Dashboard;

/// <summary>
/// The personal arrangement of one user for one dashboard: which parts are hidden and in which order the rest appears.
/// </summary>
/// <remarks>
/// A layout never grants anything: the role rules of the parts are always applied first, and ids of parts the user may not see
/// (or that no longer exist) are ignored. Parts that are not mentioned keep their default position.
/// </remarks>
public sealed class DashboardLayout
{
    /// <summary>
    /// The dashboard key used when a role center does not name one: the route of the start page.
    /// </summary>
    public const string DefaultKey = "/";

    /// <summary>
    /// One preference per part the user arranged.
    /// </summary>
    public List<DashboardPartPreference> Parts { get; set; } = [];

    /// <summary>
    /// True when the layout carries no preference at all, so every part appears in its default place.
    /// </summary>
    [JsonIgnore]
    public bool IsDefault => Parts is null || Parts.Count == 0;
}
