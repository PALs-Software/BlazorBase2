using System.Diagnostics;
using BlazorBase.User.SecurityHarness.Probes;
using BlazorBase.User.SecurityHarness.Reporting;
using Xunit;

namespace BlazorBase.User.SecurityHarness.Test;

/// <summary>
/// Runs the full probe catalogue exactly once for every test in <see cref="CiGateTests"/>, since a
/// full run starts one Kestrel host per profile and must not be repeated per assertion.
/// </summary>
public sealed class CatalogueFixture : IAsyncLifetime
{
    public HarnessReport Report { get; private set; } = null!;

    public IReadOnlyList<IAuthProbe> Probes { get; private set; } = [];

    public TimeSpan Elapsed { get; private set; }

    public async Task InitializeAsync()
    {
        Probes = ProbeCatalog.Discover();

        var stopwatch = Stopwatch.StartNew();
        Report = await HarnessRunner.RunAsync(HarnessRunOptions.Full, CancellationToken.None);
        stopwatch.Stop();
        Elapsed = stopwatch.Elapsed;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
