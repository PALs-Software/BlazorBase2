using BlazorBase.Components.IssueReport;
using Xunit;

namespace BlazorBase.Components.Test.IssueReport;

public sealed class IssueReportMailtoBuilderTests
{
    private static readonly IssueReportOptions Options = new() { Recipient = "support@example.com", SiteName = "demo" };

    private static IssueReportDiagnostics Diagnostics(int errorLength = 40) => new()
    {
        PageUrl = "https://demo.example.com/budgets",
        Route = "/budgets",
        TimeUtc = new DateTimeOffset(2026, 10, 28, 13, 5, 11, TimeSpan.Zero),
        App = "demo 1.0",
        User = "anna@example.com",
        CorrelationId = "corr-1",
        Culture = "de-DE",
        Theme = "Dark",
        Browser = "Mozilla/5.0 TestBrowser",
        Errors = [new string('a', errorLength), new string('b', errorLength)],
    };

    private static string Decode(string uri, string key)
    {
        var query = uri[(uri.IndexOf('?') + 1)..];
        var pair = query.Split('&').Single(part => part.StartsWith(key + "=", StringComparison.Ordinal));

        return Uri.UnescapeDataString(pair[(key.Length + 1)..]);
    }

    [Fact]
    public void TheSubjectCarriesTheSitePrefixCategoryAndSummary()
    {
        var mail = IssueReportMailtoBuilder.Build(Options, new IssueReportInput("List is empty", "Details", "Bug"), null);

        Assert.Equal("[demo] Bug: List is empty", mail.Subject);
        Assert.StartsWith("mailto:support@example.com?subject=", mail.MailtoUri, StringComparison.Ordinal);
        Assert.Equal(mail.Subject, Decode(mail.MailtoUri, "subject"));
    }

    [Fact]
    public void ACustomSubjectPrefixReplacesTheSiteName()
    {
        var options = new IssueReportOptions { Recipient = "a@b.c", SiteName = "demo", SubjectPrefix = "[X]" };

        var mail = IssueReportMailtoBuilder.Build(options, new IssueReportInput("Problem"), null);

        Assert.Equal("[X] Problem", mail.Subject);
    }

    [Fact]
    public void WithoutCategoryAndPrefixTheSubjectIsTheSummary()
    {
        var options = new IssueReportOptions { Recipient = "a@b.c" };

        var mail = IssueReportMailtoBuilder.Build(options, new IssueReportInput("  Problem  "), null);

        Assert.Equal("Problem", mail.Subject);
    }

    [Fact]
    public void TheBodyUsesCrLfAndContainsTheDescriptionAndAllDiagnosticLines()
    {
        var mail = IssueReportMailtoBuilder.Build(Options, new IssueReportInput("S", "Line one\nLine two"), Diagnostics());

        Assert.Contains("Description:\r\nLine one\r\nLine two", mail.Body);
        Assert.Contains("%0D%0A", mail.MailtoUri);
        Assert.Contains("Page=https://demo.example.com/budgets", mail.Body);
        Assert.Contains("Time=2026-10-28T13:05:11Z", mail.Body);
        Assert.Contains("User=anna@example.com", mail.Body);
        Assert.Contains("Correlation=corr-1", mail.Body);
        Assert.Contains("Browser=Mozilla/5.0 TestBrowser", mail.Body);
        Assert.False(mail.IsTruncated);
        Assert.False(mail.ExceedsLimit);
    }

    [Fact]
    public void NullDiagnosticsLeaveTheDiagnosticsBlockOut()
    {
        var mail = IssueReportMailtoBuilder.Build(Options, new IssueReportInput("S", "Text"), null);

        Assert.DoesNotContain("Diagnostics", mail.Body);
        Assert.False(mail.IsTruncated);
    }

    [Fact]
    public void ErrorsAreListedNewestFirstAndLimitedByTheOptions()
    {
        var diagnostics = Diagnostics() with { Errors = ["first", "second", "third", "fourth"] };
        var options = new IssueReportOptions { Recipient = "a@b.c", MaxErrorEntries = 2 };

        var mail = IssueReportMailtoBuilder.Build(options, new IssueReportInput("S"), diagnostics);

        Assert.Contains("Error1=fourth", mail.Body);
        Assert.Contains("Error2=third", mail.Body);
        Assert.DoesNotContain("second", mail.Body);
    }

    [Fact]
    public void TheLinkStaysWithinTheLimitByShorteningTheErrorsFirst()
    {
        var diagnostics = Diagnostics(errorLength: 280);
        var input = new IssueReportInput("S", "Short text");
        var fullLength = IssueReportMailtoBuilder.Build(new IssueReportOptions { Recipient = "support@example.com", SiteName = "demo", MaxUrlLength = 100000 }, input, diagnostics).MailtoUri.Length;
        var options = new IssueReportOptions { Recipient = "support@example.com", SiteName = "demo", MaxUrlLength = fullLength - 40 };

        var mail = IssueReportMailtoBuilder.Build(options, input, diagnostics);

        Assert.True(mail.MailtoUri.Length <= options.MaxUrlLength);
        Assert.True(mail.IsTruncated);
        Assert.Contains("Browser=Mozilla", mail.Body);
        Assert.Contains("…", mail.Body);
    }
    [Fact]
    public void WhenShorteningIsNotEnoughTheLeastImportantLinesGoFirst()
    {
        var options = new IssueReportOptions { Recipient = "support@example.com", SiteName = "demo", MaxUrlLength = 500 };
        var diagnostics = Diagnostics(errorLength: 280);

        var mail = IssueReportMailtoBuilder.Build(options, new IssueReportInput("S", "Short text"), diagnostics);

        Assert.True(mail.MailtoUri.Length <= 500);
        Assert.True(mail.IsTruncated);
        Assert.Contains("Page=", mail.Body);
        Assert.DoesNotContain("Browser=", mail.Body);
        Assert.DoesNotContain("Theme=", mail.Body);
    }

    [Fact]
    public void TheUsersTextIsNeverShortenedEvenWhenItAloneExceedsTheLimit()
    {
        var options = new IssueReportOptions { Recipient = "support@example.com", SiteName = "demo", MaxUrlLength = 300 };
        var text = string.Join(' ', Enumerable.Repeat("important", 120));

        var mail = IssueReportMailtoBuilder.Build(options, new IssueReportInput("S", text), Diagnostics());

        Assert.True(mail.ExceedsLimit);
        Assert.True(mail.IsTruncated);
        Assert.DoesNotContain("Diagnostics", mail.Body);
        Assert.Contains(text, Decode(mail.MailtoUri, "body"));
    }

    [Fact]
    public void ALongSummaryIsCutInTheSubjectAndRepeatedInFullInTheBody()
    {
        var summary = new string('x', 300);

        var mail = IssueReportMailtoBuilder.Build(Options, new IssueReportInput(summary, "Text"), null);

        Assert.True(mail.Subject.Length < 150);
        Assert.EndsWith("…", mail.Subject);
        Assert.Contains($"Summary: {summary}", mail.Body);
    }

    [Fact]
    public void TheClipboardTextHoldsEverythingEvenWhenTheLinkWasShortened()
    {
        var options = new IssueReportOptions { Recipient = "support@example.com", SiteName = "demo", MaxUrlLength = 500 };
        var diagnostics = Diagnostics(errorLength: 280);

        var mail = IssueReportMailtoBuilder.Build(options, new IssueReportInput("S", "Short text"), diagnostics);

        Assert.StartsWith("To: support@example.com\nSubject: [demo] S\n", mail.FullText);
        Assert.Contains("Browser=Mozilla/5.0 TestBrowser", mail.FullText);
        Assert.Contains(new string('a', 280), mail.FullText);
        Assert.DoesNotContain('\r', mail.FullText);
    }

    [Fact]
    public void SpecialCharactersAreEncodedInTheLink()
    {
        var mail = IssueReportMailtoBuilder.Build(Options, new IssueReportInput("A & B = C?", "x&y=z #1 ä"), null);

        Assert.DoesNotContain("A & B", mail.MailtoUri);
        Assert.Equal("[demo] A & B = C?", Decode(mail.MailtoUri, "subject"));
        Assert.Contains("x&y=z #1 ä", Decode(mail.MailtoUri, "body"));
    }
}
