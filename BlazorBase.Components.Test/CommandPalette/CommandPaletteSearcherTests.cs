using System.Globalization;
using System.Security.Claims;
using BlazorBase.Components.CommandPalette;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorBase.Components.Test.CommandPalette;

public sealed class CommandPaletteSearcherTests
{
    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal UserWithRoles(params string[] roles) =>
        new(new ClaimsIdentity(roles.Select(role => new Claim(ClaimTypes.Role, role)), "test"));

    private static CommandPaletteEntry Page(string id, string title, string? subtitle = null, string? role = null, string group = "Pages", int groupOrder = 100, params string[] keywords) =>
        new()
        {
            Id = id,
            Title = title,
            Subtitle = subtitle,
            Group = group,
            GroupOrder = groupOrder,
            Href = "/" + id,
            RequiredRole = role,
            Keywords = keywords.Length == 0 ? null : keywords,
        };

    private static ICommandPaletteProvider Provider(params CommandPaletteEntry[] entries) => new StaticCommandPaletteProvider(entries);

    private static CommandPaletteSearcher CreateSearcher(Action<CommandPaletteOptions>? configure = null, ILogger? logger = null)
    {
        var options = new CommandPaletteOptions();
        configure?.Invoke(options);
        return new CommandPaletteSearcher(options, logger ?? NullLogger.Instance, "Recent");
    }

    private static Task<CommandPaletteResult> SearchAsync(
        CommandPaletteSearcher searcher,
        string query,
        IEnumerable<ICommandPaletteProvider> providers,
        ClaimsPrincipal? user = null,
        IReadOnlyList<string>? recent = null,
        CancellationToken cancellationToken = default) =>
        searcher.SearchAsync([.. providers], query, user ?? Anonymous(), CultureInfo.InvariantCulture, recent ?? [], cancellationToken);

    private static List<string> Titles(CommandPaletteResult result) =>
        result.Groups.SelectMany(group => group.Entries).Select(entry => entry.Title).ToList();

    [Fact]
    public async Task StartsWith_BeatsWordStartBeatsContains()
    {
        var provider = Provider(
            Page("a", "Subudget"),
            Page("b", "Monthly Budgets"),
            Page("c", "Budgets"));

        var result = await SearchAsync(CreateSearcher(), "bud", [provider]);

        Assert.Equal(["Budgets", "Monthly Budgets", "Subudget"], Titles(result));
    }

    [Fact]
    public async Task EqualScores_ListTheShorterTitleFirst()
    {
        var provider = Provider(Page("a", "Budget boosts"), Page("b", "Budgets"), Page("c", "Budget changes"));

        var result = await SearchAsync(CreateSearcher(), "bud", [provider]);

        Assert.Equal(["Budgets", "Budget boosts", "Budget changes"], Titles(result));
    }

    [Fact]
    public async Task Matching_IgnoresCaseAndDiacritics()
    {
        var provider = Provider(Page("a", "Übersicht"), Page("b", "Cafés"));

        var result = await SearchAsync(CreateSearcher(), "UBERS", [provider]);
        Assert.Equal(["Übersicht"], Titles(result));

        result = await SearchAsync(CreateSearcher(), "cafe", [provider]);
        Assert.Equal(["Cafés"], Titles(result));
    }

    [Fact]
    public async Task Matching_FindsKeywordsAndSubtitlesBehindTheTitle()
    {
        var provider = Provider(
            Page("a", "Setups", keywords: ["configuration", "settings"]),
            Page("b", "Budgets", subtitle: "GitHub Copilot › Budgets"),
            Page("c", "Settings"));

        var settings = await SearchAsync(CreateSearcher(), "settings", [provider]);
        Assert.Equal(["Settings", "Setups"], Titles(settings));

        var copilot = await SearchAsync(CreateSearcher(), "copilot", [provider]);
        Assert.Equal(["Budgets"], Titles(copilot));
    }

    [Fact]
    public async Task AllWordsOfTheQueryMustMatch()
    {
        var provider = Provider(
            Page("a", "Budgets", subtitle: "GitHub Copilot › Budgets"),
            Page("b", "Budgets", subtitle: "Microsoft 365"),
            Page("c", "Seats", subtitle: "GitHub Copilot"));

        var result = await SearchAsync(CreateSearcher(), "copilot bud", [provider]);

        var entry = Assert.Single(result.Groups.SelectMany(group => group.Entries));
        Assert.Equal("a", entry.Id);
    }

    [Fact]
    public async Task ResultsPerGroupAndTotalAreCapped()
    {
        var pages = Enumerable.Range(1, 10).Select(index => Page($"p{index}", $"Report {index:00}", group: "Pages", groupOrder: 1));
        var actions = Enumerable.Range(1, 10).Select(index => Page($"a{index}", $"Report action {index:00}", group: "Actions", groupOrder: 2));
        var provider = Provider([.. pages, .. actions]);

        var perGroup = await SearchAsync(CreateSearcher(), "report", [provider]);
        Assert.All(perGroup.Groups, group => Assert.Equal(6, group.Entries.Count));

        var total = await SearchAsync(CreateSearcher(options => options.MaxResults = 8), "report", [provider]);
        Assert.Equal(8, total.Count);
        Assert.Equal(6, total.Groups[0].Entries.Count);
        Assert.Equal(2, total.Groups[1].Entries.Count);
    }

    [Fact]
    public async Task EmptyQuery_SuggestsAFewEntriesPerGroupInProviderOrder()
    {
        var provider = Provider([.. Enumerable.Range(1, 8).Select(index => Page($"p{index}", $"Page {index}"))]);

        var result = await SearchAsync(CreateSearcher(), string.Empty, [provider]);

        var group = Assert.Single(result.Groups);
        Assert.Equal(["Page 1", "Page 2", "Page 3", "Page 4", "Page 5"], group.Entries.Select(entry => entry.Title));
    }

    [Fact]
    public async Task GroupsAreOrderedByGroupOrderThenName()
    {
        var provider = Provider(
            Page("a", "Alpha", group: "Zebra", groupOrder: 1),
            Page("b", "Beta", group: "Apps", groupOrder: 2),
            Page("c", "Gamma", group: "Actions", groupOrder: 2));

        var result = await SearchAsync(CreateSearcher(), string.Empty, [provider]);

        Assert.Equal(["Zebra", "Actions", "Apps"], result.Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task EmptyQuery_ListsRecentEntriesFirstInRecentOrderAndOnlyOnce()
    {
        var provider = Provider(Page("a", "Alpha"), Page("b", "Beta"), Page("c", "Gamma"));

        var result = await SearchAsync(CreateSearcher(), string.Empty, [provider], recent: ["c", "gone", "a"]);

        Assert.Equal("Recent", result.Groups[0].Name);
        Assert.Equal(["Gamma", "Alpha"], result.Groups[0].Entries.Select(entry => entry.Title));
        Assert.Equal(["Beta"], result.Groups[1].Entries.Select(entry => entry.Title));
    }

    [Fact]
    public async Task RecentEntries_AreBoostedWhileTyping()
    {
        var provider = Provider(Page("a", "Budget report"), Page("b", "Budget plan"));

        var plain = await SearchAsync(CreateSearcher(), "budget", [provider]);
        Assert.Equal(["Budget plan", "Budget report"], Titles(plain));

        var boosted = await SearchAsync(CreateSearcher(), "budget", [provider], recent: ["a"]);
        Assert.Equal(["Budget report", "Budget plan"], Titles(boosted));
    }

    [Fact]
    public async Task EntriesWithoutRole_AreOfferedToAnonymousUsers()
    {
        var provider = Provider(Page("a", "Public"), Page("b", "Private", role: "Admin"));

        var result = await SearchAsync(CreateSearcher(), string.Empty, [provider], Anonymous());

        Assert.Equal(["Public"], Titles(result));
    }

    [Fact]
    public async Task EntriesWithRole_AreOfferedOnlyToUsersInOneOfTheRoles()
    {
        var provider = Provider(Page("a", "Users", role: "Admin, Auditor"), Page("b", "Home"));

        var allowed = await SearchAsync(CreateSearcher(), string.Empty, [provider], UserWithRoles("Auditor"));
        var denied = await SearchAsync(CreateSearcher(), string.Empty, [provider], UserWithRoles("Employee"));

        Assert.Equal(["Users", "Home"], Titles(allowed));
        Assert.Equal(["Home"], Titles(denied));
    }

    [Fact]
    public async Task AProviderReturningAForbiddenEntry_StillHasItDropped()
    {
        var careless = new DelegateCommandPaletteProvider((_, _) => ValueTask.FromResult<IReadOnlyList<CommandPaletteEntry>>(
            [Page("secret", "Secret budget", role: "Admin"), Page("open", "Open budget")]));

        var result = await SearchAsync(CreateSearcher(), "budget", [careless], UserWithRoles("Employee"));

        Assert.Equal(["Open budget"], Titles(result));
    }

    [Fact]
    public async Task AFailingProvider_DoesNotAffectTheOthersAndIsLogged()
    {
        var logger = new RecordingLogger();
        var broken = new DelegateCommandPaletteProvider((_, _) => throw new InvalidOperationException("boom"));
        var good = Provider(Page("a", "Budgets"));

        var result = await SearchAsync(CreateSearcher(logger: logger), "bud", [broken, good]);

        Assert.Equal(["Budgets"], Titles(result));
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception?.Message == "boom");
    }

    [Fact]
    public async Task ASlowProvider_IsSkippedAfterTheTimeout()
    {
        var slow = new DelegateCommandPaletteProvider(async (_, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            return [Page("slow", "Slow budget")];
        });
        var good = Provider(Page("a", "Budgets"));

        var result = await SearchAsync(CreateSearcher(options => options.ProviderTimeout = TimeSpan.FromMilliseconds(50)), "bud", [slow, good]);

        Assert.Equal(["Budgets"], Titles(result));
    }

    [Fact]
    public async Task ACancelledSearch_ThrowsInsteadOfReturningStaleResults()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new DelegateCommandPaletteProvider(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return [Page("a", "Budgets")];
        });

        var search = SearchAsync(CreateSearcher(), "bud", [provider], cancellationToken: cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search);
    }

    [Fact]
    public async Task Providers_ReceiveTheTrimmedQueryTheUserAndTheCulture()
    {
        CommandPaletteRequest? seen = null;
        var provider = new DelegateCommandPaletteProvider((request, _) =>
        {
            seen = request;
            return ValueTask.FromResult<IReadOnlyList<CommandPaletteEntry>>([]);
        });
        var user = UserWithRoles("Admin");

        await CreateSearcher().SearchAsync([provider], "  bud  ", user, new CultureInfo("de-DE"), [], CancellationToken.None);

        Assert.NotNull(seen);
        Assert.Equal("bud", seen.Query);
        Assert.Same(user, seen.User);
        Assert.Equal("de-DE", seen.Culture.Name);
        Assert.Equal(30, seen.MaxResults);
    }

    [Fact]
    public async Task EntriesWithoutExactlyOneTarget_AreDroppedAndLogged()
    {
        var logger = new RecordingLogger();
        var provider = Provider(
            new CommandPaletteEntry { Id = "none", Title = "No target", Group = "Pages" },
            new CommandPaletteEntry { Id = "both", Title = "Both targets", Group = "Pages", Href = "/x", Execute = () => Task.CompletedTask },
            new CommandPaletteEntry { Id = "action", Title = "Action target", Group = "Pages", Execute = () => Task.CompletedTask });

        var result = await SearchAsync(CreateSearcher(logger: logger), "target", [provider]);

        Assert.Equal(["Action target"], Titles(result));
        Assert.Equal(2, logger.Entries.Count(entry => entry.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task DuplicateIds_KeepTheFirstEntry()
    {
        var first = Provider(Page("same", "First"));
        var second = Provider(Page("same", "Second"));

        var result = await SearchAsync(CreateSearcher(), string.Empty, [first, second]);

        Assert.Equal(["First"], Titles(result));
    }

    [Fact]
    public async Task PreMatchedEntries_AreListedBelowTheTextMatches()
    {
        var server = Provider(new CommandPaletteEntry { Id = "fuzzy", Title = "Quartalsplanung", Group = "Pages", Href = "/q", PreMatched = true });
        var local = Provider(Page("a", "Budget plan"));

        var result = await SearchAsync(CreateSearcher(), "budjet", [server, local]);
        Assert.Equal(["Quartalsplanung"], Titles(result));

        var mixed = await SearchAsync(CreateSearcher(), "plan", [server, local]);
        Assert.Equal(["Budget plan", "Quartalsplanung"], Titles(mixed));
    }

    [Fact]
    public async Task NoMatchAtAll_ReturnsNoGroups()
    {
        var provider = Provider(Page("a", "Budgets"));

        var result = await SearchAsync(CreateSearcher(), "zzz", [provider]);

        Assert.Empty(result.Groups);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception));
    }
}
