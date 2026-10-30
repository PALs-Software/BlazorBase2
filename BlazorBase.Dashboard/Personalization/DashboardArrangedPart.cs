namespace BlazorBase.Dashboard;

/// <summary>
/// One part of a role center in the order a user sees it, with the flag whether the user hid it.
/// </summary>
/// <param name="Registration">The registered part.</param>
/// <param name="Hidden">True when the layout hides the part.</param>
public sealed record DashboardArrangedPart(DashboardPartRegistration Registration, bool Hidden);
