using Microsoft.AspNetCore.Components;

namespace BlazorBase.Dashboard;

/// <summary>
/// Default, thread-safe <see cref="IDashboardPartRegistry"/>.
/// </summary>
public sealed class DashboardPartRegistry : IDashboardPartRegistry
{
    private readonly Lock SyncRoot = new();
    private readonly List<DashboardPartRegistration> Registrations = [];
    private IReadOnlyList<DashboardPartRegistration>? Snapshot;

    /// <inheritdoc />
    public IReadOnlyList<DashboardPartRegistration> Parts
    {
        get
        {
            lock (SyncRoot)
                return Snapshot ??= [.. Registrations.OrderBy(registration => registration.Priority)];
        }
    }

    /// <inheritdoc />
    public void Register(DashboardPartRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (String.IsNullOrWhiteSpace(registration.Id))
            throw new InvalidOperationException("A dashboard part needs an id.");

        if (!typeof(IComponent).IsAssignableFrom(registration.ComponentType))
            throw new InvalidOperationException($"The dashboard part '{registration.Id}' is not a Blazor component: {registration.ComponentType}.");

        lock (SyncRoot)
        {
            if (Registrations.Any(existing => String.Equals(existing.Id, registration.Id, StringComparison.Ordinal)))
                throw new InvalidOperationException($"A dashboard part with the id '{registration.Id}' is already registered.");

            Registrations.Add(registration);
            Snapshot = null;
        }
    }
}
