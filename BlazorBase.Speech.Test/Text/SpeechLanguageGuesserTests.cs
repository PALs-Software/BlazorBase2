using BlazorBase.Speech.Text;
using Xunit;

namespace BlazorBase.Speech.Test.Text;

public sealed class SpeechLanguageGuesserTests
{
    [Theory]
    [InlineData("Im Sprachmodus hältst du den Knopf gedrückt und sprichst.", "de")]
    [InlineData("Ich kann dir das gerne erklären, wenn du möchtest.", "de")]
    [InlineData("In voice mode you hold the button and talk.", "en")]
    [InlineData("Sure, I can explain that to you if you want.", "en")]
    [InlineData("## Kurze Antwort\n\nDas ist **wichtig** für die Tests.", "de")]
    public void Guess_RecognizesTheLanguage(string text, string expected)
        => Assert.Equal(expected, SpeechLanguageGuesser.Guess(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OK")]
    [InlineData("dotnet test --filter Speech")]
    [InlineData("Also, in an hour I will be there.")]
    public void Guess_WithoutEnoughEvidence_ReturnsNull(string? text)
        => Assert.Null(SpeechLanguageGuesser.Guess(text));

    [Fact]
    public void Guess_ATie_ReturnsNull()
        => Assert.Null(SpeechLanguageGuesser.Guess("the der and und"));
}
