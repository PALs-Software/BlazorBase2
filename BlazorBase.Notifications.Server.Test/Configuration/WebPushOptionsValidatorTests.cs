using BlazorBase.Notifications.Server.Configuration;
using Xunit;

namespace BlazorBase.Notifications.Server.Test.Configuration;

public sealed class WebPushOptionsValidatorTests
{
    private readonly WebPushOptionsValidator Validator = new();

    private static WebPushOptions ValidOptions() => new()
    {
        Subject = "mailto:support@example.test",
        PublicKey = "test-public-key",
        PrivateKey = "test-private-key",
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Fails_ForNullOrBlankSubject(string? subject)
    {
        var options = ValidOptions();
        options.Subject = subject;

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("support@example.test")]
    [InlineData("http://example.test")]
    [InlineData("not-a-uri")]
    public void Validate_Fails_ForSubjectThatIsNeitherMailtoNorHttps(string subject)
    {
        var options = ValidOptions();
        options.Subject = subject;

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("mailto:support@example.test")]
    [InlineData("https://example.test")]
    public void Validate_Succeeds_ForMailtoOrHttpsSubject(string subject)
    {
        var options = ValidOptions();
        options.Subject = subject;

        var result = Validator.Validate(null, options);

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Fails_ForNullOrBlankPublicKey(string? publicKey)
    {
        var options = ValidOptions();
        options.PublicKey = publicKey;

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Fails_ForNullOrBlankPrivateKey(string? privateKey)
    {
        var options = ValidOptions();
        options.PrivateKey = privateKey;

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Fails_ForNonPositiveTimeToLive(int timeToLiveSeconds)
    {
        var options = ValidOptions();
        options.TimeToLiveSeconds = timeToLiveSeconds;

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_Succeeds_ForFullyValidOptions()
    {
        var result = Validator.Validate(null, ValidOptions());

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_ReportsEveryFailure_ForCompletelyEmptyOptions()
    {
        var result = Validator.Validate(null, new WebPushOptions { TimeToLiveSeconds = 0 });

        Assert.True(result.Failed);
        Assert.Equal(4, result.Failures.Count());
    }
}
