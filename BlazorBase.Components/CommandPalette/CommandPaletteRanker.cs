namespace BlazorBase.Components.CommandPalette;

/// <summary>
/// Scores palette entries against a query. Every query word must match somewhere; the score of a word is the best
/// of its matches, ordered from strongest to weakest: title starts with, keyword starts with, word in title starts
/// with, word in keyword starts with, title contains, keyword contains, subtitle starts with, subtitle word starts
/// with, subtitle contains. The score of an entry is the average over its words.
/// </summary>
internal static class CommandPaletteRanker
{
    public const int NoMatch = -1;
    public const int PreMatchedScore = 100;
    public const int ExactTitleBonus = 50;
    public const int RecentBonus = 150;

    public static int Score(CommandPaletteEntry entry, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
            return 0;

        var title = CommandPaletteText.Fold(entry.Title);
        var subtitle = CommandPaletteText.Fold(entry.Subtitle);
        var keywords = entry.Keywords?.Select(CommandPaletteText.Fold).ToList() ?? [];

        var total = 0;
        foreach (var token in tokens)
        {
            var best = ScoreToken(title, subtitle, keywords, token);
            if (best == NoMatch)
            {
                if (!entry.PreMatched)
                    return NoMatch;

                best = PreMatchedScore;
            }

            total += best;
        }

        var score = total / tokens.Count;
        return title == string.Join(' ', tokens) ? score + ExactTitleBonus : score;
    }

    private static int ScoreToken(string title, string subtitle, List<string> keywords, string token)
    {
        var best = NoMatch;

        best = Math.Max(best, title.Length == 0 ? NoMatch : TitleScore(CommandPaletteText.Match(title, token, out _)));

        foreach (var keyword in keywords)
            best = Math.Max(best, KeywordScore(CommandPaletteText.Match(keyword, token, out _)));

        best = Math.Max(best, SubtitleScore(CommandPaletteText.Match(subtitle, token, out _)));
        return best;
    }

    private static int TitleScore(MatchKind kind) => kind switch
    {
        MatchKind.Starts => 1000,
        MatchKind.WordStart => 800,
        MatchKind.Contains => 600,
        _ => NoMatch,
    };

    private static int KeywordScore(MatchKind kind) => kind switch
    {
        MatchKind.Starts => 900,
        MatchKind.WordStart => 700,
        MatchKind.Contains => 500,
        _ => NoMatch,
    };

    private static int SubtitleScore(MatchKind kind) => kind switch
    {
        MatchKind.Starts => 400,
        MatchKind.WordStart => 300,
        MatchKind.Contains => 200,
        _ => NoMatch,
    };
}
