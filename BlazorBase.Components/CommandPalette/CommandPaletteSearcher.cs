using System.Globalization;
using System.Security.Claims;
using BlazorBase.Components.Layout.Navigation;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Components.CommandPalette;

internal sealed record CommandPaletteResultGroup(string Name, IReadOnlyList<CommandPaletteEntry> Entries);

internal sealed record CommandPaletteResult(IReadOnlyList<CommandPaletteResultGroup> Groups)
{
    public static CommandPaletteResult Empty { get; } = new([]);

    public int Count => Groups.Sum(group => group.Entries.Count);
}

/// <summary>
/// Collects the entries of all providers and turns them into the grouped, permission checked, ranked and capped
/// result the palette shows. A provider that fails or takes too long never affects the others.
/// </summary>
internal sealed class CommandPaletteSearcher(CommandPaletteOptions options, ILogger logger, string recentGroupName)
{
    public async Task<CommandPaletteResult> SearchAsync(
        IReadOnlyList<ICommandPaletteProvider> providers,
        string query,
        ClaimsPrincipal user,
        CultureInfo culture,
        IReadOnlyList<string> recentIds,
        CancellationToken cancellationToken)
    {
        var request = new CommandPaletteRequest
        {
            Query = query.Trim(),
            User = user,
            Culture = culture,
            MaxResults = options.MaxResults,
        };

        var answers = await Task.WhenAll(providers.Select(provider => CollectAsync(provider, request, cancellationToken)));
        var entries = Admit(answers.SelectMany(answer => answer), user);
        var tokens = CommandPaletteText.Tokenize(request.Query);

        return tokens.Count == 0
            ? Suggest(entries, recentIds)
            : Rank(entries, tokens, recentIds);
    }

    private async Task<IReadOnlyList<CommandPaletteEntry>> CollectAsync(
        ICommandPaletteProvider provider,
        CommandPaletteRequest request,
        CancellationToken cancellationToken)
    {
        var timeout = options.ProviderTimeout > TimeSpan.Zero ? options.ProviderTimeout : Timeout.InfiniteTimeSpan;

        try
        {
            var entries = await provider.GetEntriesAsync(request, cancellationToken).AsTask().WaitAsync(timeout, cancellationToken);
            return entries ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            logger.LogWarning("The command palette provider {Provider} did not answer within {Timeout} and was skipped.", provider.GetType().Name, timeout);
            return [];
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The command palette provider {Provider} failed and was skipped.", provider.GetType().Name);
            return [];
        }
    }

    /// <summary>Drops entries without exactly one target, duplicate ids and every entry the user may not see.</summary>
    private List<CommandPaletteEntry> Admit(IEnumerable<CommandPaletteEntry?> candidates, ClaimsPrincipal user)
    {
        var admitted = new List<CommandPaletteEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in candidates)
        {
            if (entry is null)
                continue;

            if (!entry.HasValidTarget)
            {
                logger.LogWarning("The command palette entry {EntryId} must set exactly one of Href and Execute and was dropped.", entry.Id);
                continue;
            }

            if (!NavigationVisibility.IsRoleAllowed(entry.RequiredRole, user) || !seen.Add(entry.Id))
                continue;

            admitted.Add(entry);
        }

        return admitted;
    }

    private CommandPaletteResult Suggest(List<CommandPaletteEntry> entries, IReadOnlyList<string> recentIds)
    {
        var groups = new List<CommandPaletteResultGroup>();
        var budget = options.MaxResults;

        var recent = recentIds
            .Distinct(StringComparer.Ordinal)
            .Select(id => entries.Find(entry => entry.Id == id))
            .OfType<CommandPaletteEntry>()
            .Take(Math.Min(options.MaxRecent, budget))
            .ToList();

        if (recent.Count > 0)
        {
            groups.Add(new CommandPaletteResultGroup(recentGroupName, recent));
            budget -= recent.Count;
        }

        var recentSet = recent.ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var group in OrderedGroups(entries.Where(entry => !recentSet.Contains(entry))))
        {
            if (budget <= 0)
                break;

            var take = group.Entries.Take(Math.Min(options.MaxSuggestionsPerGroup, budget)).ToList();
            groups.Add(group with { Entries = take });
            budget -= take.Count;
        }

        return new CommandPaletteResult(groups);
    }

    private CommandPaletteResult Rank(List<CommandPaletteEntry> entries, IReadOnlyList<string> tokens, IReadOnlyList<string> recentIds)
    {
        var recentPosition = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in recentIds)
            recentPosition.TryAdd(id, recentPosition.Count);

        var scores = new Dictionary<CommandPaletteEntry, int>(ReferenceEqualityComparer.Instance);
        foreach (var entry in entries)
        {
            var score = CommandPaletteRanker.Score(entry, tokens);
            if (score == CommandPaletteRanker.NoMatch)
                continue;

            if (recentPosition.TryGetValue(entry.Id, out var position))
                score += CommandPaletteRanker.RecentBonus - 10 * position;

            scores[entry] = score;
        }

        var groups = new List<CommandPaletteResultGroup>();
        var budget = options.MaxResults;
        foreach (var group in OrderedGroups(scores.Keys))
        {
            if (budget <= 0)
                break;

            var ranked = group.Entries
                .OrderByDescending(entry => scores[entry])
                .ThenBy(entry => entry.Title.Length)
                .ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .Take(Math.Min(options.MaxResultsPerGroup, budget))
                .ToList();

            groups.Add(group with { Entries = ranked });
            budget -= ranked.Count;
        }

        return new CommandPaletteResult(groups);
    }

    private static IEnumerable<CommandPaletteResultGroup> OrderedGroups(IEnumerable<CommandPaletteEntry> entries) =>
        entries
            .GroupBy(entry => entry.Group, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(group => group.Min(entry => entry.GroupOrder))
            .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new CommandPaletteResultGroup(group.Key, group.ToList()));
}
