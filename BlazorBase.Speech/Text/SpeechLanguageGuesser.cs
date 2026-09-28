using System.Text.RegularExpressions;

namespace BlazorBase.Speech.Text;

/// <summary>
/// Tells German from English by counting frequent function words, so an answer is read with a voice of
/// its own language. The UI language is no substitute: people talk in the language they think in, and an
/// English voice reading a German answer is barely intelligible.
/// </summary>
/// <remarks>
/// Only words that exist in just one of the two languages count ("in", "an", "also", "will" do not).
/// Too few hits or a tie yield <see langword="null"/>, leaving the choice to the caller's fallback.
/// </remarks>
public static partial class SpeechLanguageGuesser
{
    private const int MinimumHits = 2;

    private static readonly HashSet<string> GermanWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "der", "die", "das", "und", "ist", "nicht", "ich", "du", "sie", "wir", "ihr", "ein", "eine", "einen", "einem",
        "mit", "für", "auf", "es", "zu", "den", "dem", "des", "von", "im", "sind", "wird", "werden", "kann", "oder",
        "aber", "wenn", "dass", "hier", "noch", "nur", "sich", "bitte", "auch", "wie", "was", "dir", "mir", "dich", "mich"
    };

    private static readonly HashSet<string> EnglishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "is", "not", "you", "we", "they", "a", "with", "for", "on", "it", "to", "of", "are", "can",
        "or", "but", "if", "that", "this", "here", "still", "only", "please", "your", "be", "how", "what", "me", "my"
    };

    /// <summary>Returns <c>de</c>, <c>en</c>, or <see langword="null"/> when the text does not tell.</summary>
    public static string? Guess(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var germanHits = 0;
        var englishHits = 0;

        foreach (Match word in Word().Matches(text))
        {
            if (GermanWords.Contains(word.Value))
                germanHits++;

            if (EnglishWords.Contains(word.Value))
                englishHits++;
        }

        if (Math.Max(germanHits, englishHits) < MinimumHits || germanHits == englishHits)
            return null;

        return germanHits > englishHits ? "de" : "en";
    }

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex Word();
}
