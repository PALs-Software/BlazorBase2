using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using BlazorBase.User.SecurityHarness.Probes;
using BlazorBase.User.SecurityHarness.Reporting;
using Microsoft.Extensions.DependencyInjection;

var options = CliOptions.Parse(args);
var probes = ProbeCatalog.Discover();

if (options.ListOnly)
{
    var listReport = new HarnessReport(DateTimeOffset.UtcNow, [], BuildListEntries(probes));
    Console.WriteLine(RenderReport(listReport, options));
    return 0;
}

if (options.BaseUrl is not null)
{
    Console.WriteLine(RenderReport(BuildEmptyForeignHostReport(), options));
    return 0;
}

var tempDirectory = Directory.CreateTempSubdirectory("blazorbase-security-harness-");
var certificate = HarnessSelfSignedCertificate.Create();
var identityProvider = await FakeIdentityProviderHost.StartAsync(certificate, CancellationToken.None);

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

    var profilesToRun = ResolveProfiles(options);
    var hostEntries = new List<HarnessHostReportEntry>();
    var probeEntries = new List<ProbeReportEntry>();

    foreach (var profile in profilesToRun)
    {
        var hostResult = await HarnessHostFactory.StartAsync(profile, hostOptions, CancellationToken.None);
        hostEntries.Add(ToHostEntry(hostResult));

        var context = BuildProbeContext(hostResult, profile, hostOptions);

        foreach (var probe in probes.Where(probe => probe.Profile == profile && MatchesFilter(probe, options)))
        {
            var probeResult = await RunProbeSafelyAsync(probe, context);
            probeEntries.Add(ToProbeEntry(probe, probeResult));
        }

        if (hostResult.Host is not null)
        {
            await hostResult.Host.StopAsync();
            hostResult.Host.Dispose();
        }
    }

    var report = new HarnessReport(DateTimeOffset.UtcNow, hostEntries, probeEntries);
    var rendered = RenderReport(report, options);

    if (options.OutputPath is not null)
        await File.WriteAllTextAsync(options.OutputPath, rendered);
    else
        Console.WriteLine(rendered);

    return report.FailedCount > 0 ? 1 : 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Harness error: {ex}");
    return 2;
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

static List<ProbeReportEntry> BuildListEntries(IReadOnlyList<IAuthProbe> probes) =>
    [.. probes.Select(probe => new ProbeReportEntry(
        probe.Id,
        probe.Category,
        probe.Attack,
        probe.Expectation,
        probe.Profile.ToString(),
        probe.SeverityIfFailed.ToString(),
        nameof(ProbeOutcome.Skipped),
        "Not run - listed with --list.",
        []))];

/// <summary>
/// No probe declares itself safe to run against a foreign host yet, since only step 3 onward adds
/// probes; returns an honest empty report rather than guessing which of today's zero probes would
/// be safe.
/// </summary>
static HarnessReport BuildEmptyForeignHostReport() => new(DateTimeOffset.UtcNow, [], []);

static IReadOnlyList<HarnessProfile> ResolveProfiles(CliOptions options)
{
    if (string.IsNullOrWhiteSpace(options.Profile) || options.Profile.Equals("all", StringComparison.OrdinalIgnoreCase))
        return Enum.GetValues<HarnessProfile>();

    if (!Enum.TryParse<HarnessProfile>(options.Profile, ignoreCase: true, out var profile))
        throw new InvalidOperationException($"Unknown --profile value '{options.Profile}'. Valid values: all, {string.Join(", ", Enum.GetNames<HarnessProfile>())}.");

    return [profile];
}

static bool MatchesFilter(IAuthProbe probe, CliOptions options)
{
    if (options.Category is not null && !string.Equals(probe.Category, options.Category, StringComparison.OrdinalIgnoreCase))
        return false;

    if (options.Id is not null && !string.Equals(probe.Id, options.Id, StringComparison.OrdinalIgnoreCase))
        return false;

    return true;
}

static async Task<ProbeResult> RunProbeSafelyAsync(IAuthProbe probe, ProbeContext context)
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
static ProbeContext BuildProbeContext(HarnessHostResult hostResult, HarnessProfile profile, HarnessHostOptions hostOptions)
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
static async Task<IsolatedProbeHost> StartIsolatedHostAsync(HarnessProfile profile, HarnessHostOptions baseOptions, IReadOnlyDictionary<string, string?> overrides, CancellationToken cancellationToken)
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

static ProbeReportEntry ToProbeEntry(IAuthProbe probe, ProbeResult result) => new(
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
static string FormatEvidence(ProbeEvidence evidence)
{
    var parts = new List<string> { evidence.Description };

    if (evidence.Request is not null)
        parts.Add($"Request: {evidence.Request}");

    if (evidence.Response is not null)
        parts.Add($"Response: {evidence.Response}");

    return string.Join(" || ", parts);
}

static HarnessHostReportEntry ToHostEntry(HarnessHostResult result) => new(
    result.Profile.ToString(),
    result.Description,
    result.Status.ToString(),
    result.BaseAddress?.ToString(),
    result.FailureMessage);

static string RenderReport(HarnessReport report, CliOptions options)
{
    IHarnessReporter reporter = options.Format switch
    {
        ReportFormat.Markdown => new MarkdownHarnessReporter(),
        ReportFormat.Json => new JsonHarnessReporter(),
        _ => new ConsoleHarnessReporter(),
    };

    return reporter.Render(report);
}

/// <summary>Hand-parsed command line for the harness - eight options do not need a package.</summary>
internal sealed class CliOptions
{
    public string? Profile { get; private init; }
    public string? Category { get; private init; }
    public string? Id { get; private init; }
    public ReportFormat Format { get; private init; } = ReportFormat.Console;
    public string? OutputPath { get; private init; }
    public string? BaseUrl { get; private init; }
    public bool ListOnly { get; private init; }

    public static CliOptions Parse(string[] args)
    {
        string? profile = null;
        string? category = null;
        string? id = null;
        var format = ReportFormat.Console;
        string? outputPath = null;
        string? baseUrl = null;
        var listOnly = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--profile":
                    profile = RequireValue(args, ref index, "--profile");
                    break;
                case "--category":
                    category = RequireValue(args, ref index, "--category");
                    break;
                case "--id":
                    id = RequireValue(args, ref index, "--id");
                    break;
                case "--format":
                    format = ParseFormat(RequireValue(args, ref index, "--format"));
                    break;
                case "--output":
                    outputPath = RequireValue(args, ref index, "--output");
                    break;
                case "--base-url":
                    baseUrl = RequireValue(args, ref index, "--base-url");
                    break;
                case "--list":
                    listOnly = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown argument '{args[index]}'.");
            }
        }

        return new CliOptions
        {
            Profile = profile,
            Category = category,
            Id = id,
            Format = format,
            OutputPath = outputPath,
            BaseUrl = baseUrl,
            ListOnly = listOnly,
        };
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new InvalidOperationException($"'{optionName}' requires a value.");

        index++;
        return args[index];
    }

    private static ReportFormat ParseFormat(string value) => value.ToLowerInvariant() switch
    {
        "console" => ReportFormat.Console,
        "markdown" => ReportFormat.Markdown,
        "json" => ReportFormat.Json,
        _ => throw new InvalidOperationException($"Unknown --format value '{value}'. Valid values: console, markdown, json."),
    };
}
