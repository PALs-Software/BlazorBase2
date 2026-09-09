using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using BlazorBase.User.SecurityHarness.Probes;
using BlazorBase.User.SecurityHarness.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.SecurityHarness;

/// <summary>Which of the catalogue's probes one <see cref="HarnessRunner"/> run should attempt.</summary>
internal sealed record HarnessRunOptions(
    IReadOnlyList<HarnessProfile>? Profiles = null,
    string? Category = null,
    string? Id = null)
{
    /// <summary>Every profile, every probe - what <c>--profile all</c> runs.</summary>
    public static readonly HarnessRunOptions Full = new();
}

/// <summary>
/// Runs the full harness orchestration once: starts the fake identity provider, starts every
/// requested profile's own Kestrel host in turn, runs every matching probe against it, and tears
/// everything down again. Extracted out of <c>Program.cs</c> so the CI wrapper can drive exactly
/// one run per test class through this same sequence instead of duplicating it.
/// </summary>
internal static class HarnessRunner
{
    public static async Task<HarnessReport> RunAsync(HarnessRunOptions options, CancellationToken cancellationToken)
    {
        var probes = ProbeCatalog.Discover();
        var tempDirectory = Directory.CreateTempSubdirectory("blazorbase-security-harness-");
        var certificate = HarnessSelfSignedCertificate.Create();
        var identityProvider = await FakeIdentityProviderHost.StartAsync(certificate, cancellationToken);

        try
        {
            var hostOptions = new HarnessHostOptions
            {
                TempDirectory = tempDirectory.FullName,
                ExternalAuthority = identityProvider.TenantAuthority(FakeIdentityProviderTenants.AllowedTenantId),
                ExternalAllowedTenantId = FakeIdentityProviderTenants.AllowedTenantId,
                IdentityProviderHost = identityProvider,
                Certificate = certificate,
            };

            var profilesToRun = options.Profiles ?? Enum.GetValues<HarnessProfile>();
            var hostEntries = new List<HarnessHostReportEntry>();
            var probeEntries = new List<ProbeReportEntry>();

            foreach (var profile in profilesToRun)
            {
                var hostResult = await HarnessHostFactory.StartAsync(profile, hostOptions, cancellationToken);
                hostEntries.Add(ToHostEntry(hostResult));

                var context = BuildProbeContext(hostResult, profile, hostOptions);

                foreach (var probe in probes.Where(probe => probe.Profile == profile && MatchesFilter(probe, options)))
                {
                    var probeResult = await RunProbeSafelyAsync(probe, context);
                    probeEntries.Add(ToProbeEntry(probe, probeResult));
                }

                if (hostResult.Host is not null)
                {
                    await hostResult.Host.StopAsync(cancellationToken);
                    hostResult.Host.Dispose();
                }
            }

            return new HarnessReport(DateTimeOffset.UtcNow, hostEntries, probeEntries);
        }
        finally
        {
            await identityProvider.DisposeAsync();

            try
            {
                Directory.Delete(tempDirectory.FullName, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static bool MatchesFilter(IAuthProbe probe, HarnessRunOptions options)
    {
        if (options.Category is not null && !string.Equals(probe.Category, options.Category, StringComparison.OrdinalIgnoreCase))
            return false;

        if (options.Id is not null && !string.Equals(probe.Id, options.Id, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static async Task<ProbeResult> RunProbeSafelyAsync(IAuthProbe probe, ProbeContext context)
    {
        try
        {
            return await probe.RunAsync(context, CancellationToken.None);
        }
        catch (Exception ex)
        {
            return ProbeResult.Inconclusive($"Probe threw before it could reach a conclusion: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the context handed to one profile's probes: a normal, fully live context when the host
    /// started, or a degenerate one carrying only <see cref="HarnessHostResult.FailureMessage"/> through
    /// <see cref="ProbeContext.StartupGuardFailure"/> when it never served a request - so a probe that
    /// targets a host that fails by design (<c>CSRF-GUARD-FIRES</c> against
    /// <see cref="HarnessProfile.CookieWithoutCsrfMiddleware"/>) still gets a context to reach a verdict
    /// from, without any of its other members ever being called.
    /// </summary>
    private static ProbeContext BuildProbeContext(HarnessHostResult hostResult, HarnessProfile profile, HarnessHostOptions hostOptions)
    {
        if (hostResult.Status == HarnessHostStatus.Started && hostResult.Host is not null && hostResult.BaseAddress is not null && hostResult.HttpClientFactory is not null)
        {
            return new ProbeContext(
                hostResult.BaseAddress,
                hostResult.HttpClientFactory,
                hostResult.Host.Services,
                hostResult.Host.Services.GetRequiredService<TimeProvider>(),
                (overrides, isolatedCancellationToken) => StartIsolatedHostAsync(profile, hostOptions, overrides, isolatedCancellationToken));
        }

        return new ProbeContext(
            new Uri("https://unavailable.invalid/"),
            () => new HttpClient(),
            new ServiceCollection().BuildServiceProvider(),
            TimeProvider.System,
            (_, _) => throw new NotSupportedException($"Profile '{profile}' never started; isolated hosts are unavailable for it."),
            hostResult.FailureMessage);
    }

    /// <summary>
    /// Starts a private instance of <paramref name="profile"/> with <paramref name="overrides"/> layered
    /// over <paramref name="baseOptions"/>'s own configuration, on its own port and its own database
    /// file - see <see cref="ProbeContext.StartIsolatedHostAsync"/>.
    /// </summary>
    private static async Task<IsolatedProbeHost> StartIsolatedHostAsync(HarnessProfile profile, HarnessHostOptions baseOptions, IReadOnlyDictionary<string, string?> overrides, CancellationToken cancellationToken)
    {
        var isolatedOptions = new HarnessHostOptions
        {
            TempDirectory = baseOptions.TempDirectory,
            ExternalAuthority = baseOptions.ExternalAuthority,
            ExternalAllowedTenantId = baseOptions.ExternalAllowedTenantId,
            IdentityProviderHost = baseOptions.IdentityProviderHost,
            Certificate = baseOptions.Certificate,
            ConfigurationOverrides = overrides,
            DatabaseFileName = $"{profile}-{Guid.NewGuid():N}.db",
        };

        var result = await HarnessHostFactory.StartAsync(profile, isolatedOptions, cancellationToken);

        if (result.Status != HarnessHostStatus.Started || result.Host is null || result.BaseAddress is null || result.HttpClientFactory is null)
            throw new InvalidOperationException($"Isolated host for profile '{profile}' failed to start: {result.FailureMessage}");

        var isolatedContext = new ProbeContext(
            result.BaseAddress,
            result.HttpClientFactory,
            result.Host.Services,
            result.Host.Services.GetRequiredService<TimeProvider>(),
            (nestedOverrides, nestedCancellationToken) => StartIsolatedHostAsync(profile, baseOptions, nestedOverrides, nestedCancellationToken));

        return new IsolatedProbeHost(isolatedContext, async () =>
        {
            await result.Host.StopAsync(cancellationToken);
            result.Host.Dispose();
        });
    }

    private static ProbeReportEntry ToProbeEntry(IAuthProbe probe, ProbeResult result) => new(
        probe.Id,
        probe.Category,
        probe.Attack,
        probe.Expectation,
        probe.Profile.ToString(),
        probe.SeverityIfFailed.ToString(),
        result.Outcome.ToString(),
        result.Summary,
        [.. result.Evidence.Select(FormatEvidence)]);

    /// <summary>
    /// Folds one <see cref="ProbeEvidence"/>'s request and response into its description, so a rendered
    /// report shows the exact token and response a probe sent and got back rather than only its
    /// one-line description - a rejection with no visible token is not usable in a review.
    /// </summary>
    private static string FormatEvidence(ProbeEvidence evidence)
    {
        var parts = new List<string> { evidence.Description };

        if (evidence.Request is not null)
            parts.Add($"Request: {evidence.Request}");

        if (evidence.Response is not null)
            parts.Add($"Response: {evidence.Response}");

        return string.Join(" || ", parts);
    }

    private static HarnessHostReportEntry ToHostEntry(HarnessHostResult result) => new(
        result.Profile.ToString(),
        result.Description,
        result.Status.ToString(),
        result.BaseAddress?.ToString(),
        result.FailureMessage);
}
