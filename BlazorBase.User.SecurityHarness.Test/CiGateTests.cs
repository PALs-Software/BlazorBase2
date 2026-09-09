using BlazorBase.User.SecurityHarness.Baseline;
using BlazorBase.User.SecurityHarness.Probes;
using Xunit;

namespace BlazorBase.User.SecurityHarness.Test;

/// <summary>
/// The CI wrapper around one full catalogue run, shared across every test in this class through
/// <see cref="CatalogueFixture"/>. Fails the build on a regression or an unbaselined new failure,
/// never on a failure the checked-in baseline already names.
/// </summary>
public sealed class CiGateTests : IClassFixture<CatalogueFixture>
{
    private readonly CatalogueFixture _fixture;

    public CiGateTests(CatalogueFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void EveryProbeCarriesItsOwnDocumentation()
    {
        foreach (var probe in _fixture.Probes)
        {
            Assert.False(string.IsNullOrWhiteSpace(probe.Id), $"Probe type '{probe.GetType().Name}' has an empty Id.");
            Assert.False(string.IsNullOrWhiteSpace(probe.Category), $"Probe '{probe.Id}' has an empty Category.");
            Assert.False(string.IsNullOrWhiteSpace(probe.Attack), $"Probe '{probe.Id}' has an empty Attack.");
            Assert.False(string.IsNullOrWhiteSpace(probe.Expectation), $"Probe '{probe.Id}' has an empty Expectation.");
        }
    }

    [Fact]
    public void EveryProbeIdIsUnique()
    {
        var duplicateIds = _fixture.Probes
            .GroupBy(probe => probe.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(duplicateIds.Count == 0, $"Duplicate probe ids: {string.Join(", ", duplicateIds)}.");
    }

    [Fact]
    public void BaselineNamesNoProbeThatNoLongerExists()
    {
        var probeIds = _fixture.Probes.Select(probe => probe.Id).ToHashSet(StringComparer.Ordinal);
        var staleEntries = HarnessBaseline.Load().Where(entry => !probeIds.Contains(entry.ProbeId)).Select(entry => entry.ProbeId).ToList();

        Assert.True(staleEntries.Count == 0, $"Baseline names probe(s) that no longer exist: {string.Join(", ", staleEntries)}.");
    }

    [Fact]
    public void NoProbeFailsWithoutABaselineEntry()
    {
        var baselinedIds = HarnessBaseline.Load().Select(entry => entry.ProbeId).ToHashSet(StringComparer.Ordinal);
        var unbaselinedFailures = _fixture.Report.Probes
            .Where(probe => probe.Outcome == nameof(ProbeOutcome.Failed) && !baselinedIds.Contains(probe.Id))
            .Select(probe => probe.Id)
            .ToList();

        Assert.True(unbaselinedFailures.Count == 0,
            $"Probe(s) failed with no baseline entry - either a regression, or a genuinely new finding: {string.Join(", ", unbaselinedFailures)}.");
    }

    [Fact]
    public void FullCatalogueRunCompletesInAReasonableTime()
    {
        Assert.True(_fixture.Elapsed < TimeSpan.FromMinutes(5),
            $"A full catalogue run took {_fixture.Elapsed.TotalSeconds:F1}s, which is too long for a CI gate - report this rather than silently raising the bound.");
    }
}
