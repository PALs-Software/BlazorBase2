namespace BlazorBase.Dashboard;

/// <summary>
/// A dashboard key or layout is not acceptable: empty, too long or too large.
/// </summary>
public sealed class DashboardLayoutValidationException(string message) : Exception(message);
