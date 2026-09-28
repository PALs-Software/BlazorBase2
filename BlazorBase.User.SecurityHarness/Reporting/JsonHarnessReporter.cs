using System.Text.Json;

namespace BlazorBase.User.SecurityHarness.Reporting;

/// <summary>JSON reporter, so a CI wrapper (step 11) can assert against the run's outcome.</summary>
public sealed class JsonHarnessReporter : IHarnessReporter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string Render(HarnessReport report) => JsonSerializer.Serialize(report, Options);
}
