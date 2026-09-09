namespace BlazorBase.User.SecurityHarness.Baseline;

/// <summary>One accepted, already-reviewed probe failure: its id and why the team accepts it as-is.</summary>
internal sealed record BaselineEntry(string ProbeId, string Reason);

/// <summary>
/// The checked-in list of probe failures the team has already reviewed and accepted, embedded from
/// <c>known-findings.baseline</c>. The CI wrapper fails a probe only when it fails AND is missing
/// from this list - never on a failure this list already names.
/// </summary>
internal static class HarnessBaseline
{
    private const string ResourceSuffix = "known-findings.baseline";

    public static IReadOnlyList<BaselineEntry> Load()
    {
        var assembly = typeof(HarnessBaseline).Assembly;
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No embedded resource ending in '{ResourceSuffix}' was found in {assembly.FullName}.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    private static IReadOnlyList<BaselineEntry> Parse(string content)
    {
        var entries = new List<BaselineEntry>();

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            entries.Add(ParseLine(line));
        }

        return entries;
    }

    private static BaselineEntry ParseLine(string line)
    {
        var separatorIndex = line.IndexOf("::", StringComparison.Ordinal);

        if (separatorIndex < 0)
            throw new FormatException($"Baseline line '{line}' is missing the '::' separator between the probe id and its reason.");

        var probeId = line[..separatorIndex].Trim();
        var reason = line[(separatorIndex + 2)..].Trim();

        if (probeId.Length == 0 || reason.Length == 0)
            throw new FormatException($"Baseline line '{line}' needs both a non-empty probe id and a non-empty reason.");

        return new BaselineEntry(probeId, reason);
    }
}
