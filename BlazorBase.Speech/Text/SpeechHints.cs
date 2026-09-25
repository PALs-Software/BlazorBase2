namespace BlazorBase.Speech.Text;

/// <summary>The spoken stand-ins for content that cannot be read aloud, in one language.</summary>
/// <param name="CodeBlock">Spoken instead of a fenced code block.</param>
/// <param name="Table">Spoken instead of a table.</param>
/// <param name="Link">Spoken instead of a bare URL.</param>
public record SpeechHints(string CodeBlock, string Table, string Link);
