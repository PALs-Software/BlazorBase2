using BlazorBase.User.SecurityHarness;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes;
using BlazorBase.User.SecurityHarness.Reporting;

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

try
{
    var runOptions = new HarnessRunOptions(ResolveProfiles(options), options.Category, options.Id);
    var report = await HarnessRunner.RunAsync(runOptions, CancellationToken.None);
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
