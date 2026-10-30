namespace BlazorBase.Dashboard;

/// <summary>
/// What one user chose for one dashboard part: whether it is hidden and where it sits in the order.
/// </summary>
public sealed class DashboardPartPreference
{
    /// <summary>
    /// The id of the part, as registered with <see cref="DashboardPartRegistration.Id"/>.
    /// </summary>
    public string PartId { get; set; } = String.Empty;

    /// <summary>
    /// True when the user hid the part. A hidden part is neither rendered nor fetched.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// Position among the parts of the layout; lower values come first. Replaces the registered priority for this user.
    /// </summary>
    public int Order { get; set; }
}
