using System.Reflection;

namespace BlazorBase.User.SecurityHarness.Probes;

/// <summary>
/// Discovers every <see cref="IAuthProbe"/> in the harness assembly by reflection, so adding a
/// probe in a later step is only ever adding a file - nothing registers it by hand.
/// </summary>
public static class ProbeCatalog
{
    public static IReadOnlyList<IAuthProbe> Discover()
    {
        return typeof(ProbeCatalog).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IAuthProbe).IsAssignableFrom(type))
            .Select(type => (IAuthProbe)(Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not construct probe type '{type.FullName}'. Every {nameof(IAuthProbe)} needs a public parameterless constructor.")))
            .OrderBy(probe => probe.Category, StringComparer.Ordinal)
            .ThenBy(probe => probe.Id, StringComparer.Ordinal)
            .ToList();
    }
}
