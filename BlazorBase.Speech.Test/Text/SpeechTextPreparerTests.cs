using BlazorBase.Speech.Test.Infrastructure;
using BlazorBase.Speech.Text;
using Xunit;

namespace BlazorBase.Speech.Test.Text;

public sealed class SpeechTextPreparerTests
{
    private static SpeechTextPreparer CreatePreparer() => new(Localizers.For<SpeechTextPreparer>());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    [InlineData("---\n\n***")]
    public void Prepare_NothingSpeakable_ReturnsNoSegments(string? markdown)
        => Assert.Empty(CreatePreparer().Prepare(markdown));

    [Fact]
    public void Prepare_FirstSentenceTravelsAlone_RestIsGrouped()
    {
        var segments = CreatePreparer().Prepare("Das ist der erste Satz. Das ist der zweite. Und der dritte!");

        Assert.Equal(["Das ist der erste Satz.", "Das ist der zweite. Und der dritte!"], segments);
    }

    [Fact]
    public void Prepare_RemovesMarkupButKeepsTheWords()
    {
        var segments = CreatePreparer().Prepare("# Überschrift\n\nDas ist **fett**, *kursiv* und ~~weg~~ gewesen.\n> Zitat hier.");

        Assert.Equal(["Überschrift.", "Das ist fett, kursiv und weg gewesen. Zitat hier."], segments);
    }

    [Fact]
    public void Prepare_ListItemsBecomeSentences()
    {
        var segments = CreatePreparer().Prepare("Schritte:\n- Repository klonen\n- Branch anlegen\n1. Tests laufen lassen\n- [x] Erledigt");

        Assert.Equal(["Schritte:", "Repository klonen. Branch anlegen. Tests laufen lassen. Erledigt."], segments);
    }

    [Fact]
    public void Prepare_LinksKeepTheirText_BareUrlsBecomeAWord()
    {
        using var culture = new CultureScope("en");

        var segments = CreatePreparer().Prepare("See [the docs](https://example.com/a_(b)) or https://example.com/x?y=1 for details.");

        Assert.Equal(["See the docs or link for details."], segments);
    }

    [Fact]
    public void Prepare_InlineCodeIsReadAsText()
    {
        var segments = CreatePreparer().Prepare("Rufe `dotnet test` im Ordner auf.");

        Assert.Equal(["Rufe dotnet test im Ordner auf."], segments);
    }

    [Fact]
    public void Prepare_IdentifiersWithUnderscoresAreNotEmphasis()
    {
        var segments = CreatePreparer().Prepare("Setze my_variable_name auf true.");

        Assert.Equal(["Setze my_variable_name auf true."], segments);
    }

    [Fact]
    public void Prepare_CodeBlocksAreReplacedByOneHint_InTheUiLanguage()
    {
        using var culture = new CultureScope("de");

        var segments = CreatePreparer().Prepare("Hier der Fix:\n```csharp\nvar x = 1;\n```\n```\nmore();\n```\nFertig.");

        Assert.Equal(["Hier der Fix:", "Hier folgt ein Codeblock, siehe die geschriebene Antwort. Fertig."], segments);
    }

    [Fact]
    public void Prepare_HintsFollowTheReadingLanguage_NotTheUiLanguage()
    {
        using var culture = new CultureScope("en");

        var segments = CreatePreparer().Prepare("Hier der Fix:\n```\nfix();\n```\nSiehe https://example.com.", "de");

        Assert.Equal(["Hier der Fix:", "Hier folgt ein Codeblock, siehe die geschriebene Antwort. Siehe Link."], segments);
        Assert.Equal("en", System.Globalization.CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void Prepare_UnknownReadingLanguage_FallsBackToTheUiLanguage()
    {
        using var culture = new CultureScope("en");

        var segments = CreatePreparer().Prepare("Look:\n```\nx\n```", "xx-invalid-culture");

        Assert.Equal(["Look:", "A code block follows, see the written answer."], segments);
    }

    [Fact]
    public void Prepare_UnterminatedCodeBlockSwallowsTheRest()
    {
        using var culture = new CultureScope("en");

        var segments = CreatePreparer().Prepare("Look:\n~~~\nsecret();\nstill code");

        Assert.Equal(["Look:", "A code block follows, see the written answer."], segments);
    }

    [Fact]
    public void Prepare_RepeatedSentencesAreAllRead()
    {
        var segments = CreatePreparer().Prepare("Nein. Nein. Nein.");

        Assert.Equal(["Nein.", "Nein. Nein."], segments);
    }

    [Fact]
    public void Prepare_TablesAreReplacedByOneHint()
    {
        using var culture = new CultureScope("en");

        var segments = CreatePreparer().Prepare("Summary\n| Name | Value |\n|---|---|\n| A | 1 |\nDone.");

        Assert.Equal(["Summary.", "A table follows, see the written answer. Done."], segments);
    }

    [Theory]
    [InlineData("Das gilt z. B. für Tests. Danach geht es weiter.", "Das gilt z. B. für Tests.")]
    [InlineData("Das gilt bzw. galt nie. Danach geht es weiter.", "Das gilt bzw. galt nie.")]
    [InlineData("Wir treffen uns am 3. Oktober im Büro. Danach geht es weiter.", "Wir treffen uns am 3. Oktober im Büro.")]
    [InlineData("Use e.g. a mock here. Then continue.", "Use e.g. a mock here.")]
    public void Prepare_AbbreviationsAndOrdinalsDoNotEndASentence(string text, string firstSegment)
        => Assert.Equal(firstSegment, CreatePreparer().Prepare(text)[0]);

    [Fact]
    public void Prepare_SplitsOverlongSentencesAtAClause()
    {
        var clause = string.Join(' ', Enumerable.Repeat("wort", 30));
        var sentence = $"{clause}, {clause}, {clause}.";

        var segments = CreatePreparer().Prepare(sentence);

        Assert.True(segments.Count > 1);
        Assert.All(segments, segment => Assert.True(segment.Length <= SpeechTextPreparer.MaximumSegmentLength));
        Assert.Equal(sentence.Replace(",", string.Empty), string.Join(' ', segments).Replace(",", string.Empty));
    }

    [Fact]
    public void Prepare_GroupsLaterSentencesUpToTheSegmentLimit()
    {
        var sentences = Enumerable.Range(1, 40).Select(index => $"Das ist Satz Nummer {index} mit etwas Text.");

        var segments = CreatePreparer().Prepare(string.Join(' ', sentences));

        Assert.Equal("Das ist Satz Nummer 1 mit etwas Text.", segments[0]);
        Assert.All(segments, segment => Assert.True(segment.Length <= SpeechTextPreparer.MaximumSegmentLength));
        Assert.True(segments.Count < 40);
    }
}
