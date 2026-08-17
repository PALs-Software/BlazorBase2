using BlazorBase.User.Server.AccessTokens;
using Xunit;

namespace BlazorBase.User.Server.Test.AccessTokens;

public class AccessTokenSettingsValidatorTests
{
    private static AccessTokenSettingsValidator Validator => new();

    [Fact]
    public void A_normal_prefix_is_accepted()
    {
        Assert.True(Validator.Validate(null, new AccessTokenSettings { SecretPrefix = "ygop_" }).Succeeded);
    }

    [Fact]
    public void An_empty_prefix_is_rejected()
    {
        Assert.True(Validator.Validate(null, new AccessTokenSettings { SecretPrefix = string.Empty }).Failed);
    }

    /// <summary>
    /// 24 characters of prefix plus the 8-character lookup body is exactly the column width; one
    /// more must fail at startup rather than truncate at the first mint.
    /// </summary>
    [Fact]
    public void A_prefix_that_exactly_fills_the_column_is_accepted()
    {
        Assert.True(Validator.Validate(null, new AccessTokenSettings { SecretPrefix = new string('p', 24) }).Succeeded);
    }

    [Fact]
    public void A_prefix_one_character_too_long_is_rejected()
    {
        var result = Validator.Validate(null, new AccessTokenSettings { SecretPrefix = new string('p', 25) });

        Assert.True(result.Failed);
        Assert.Contains("too long", result.FailureMessage);
    }
}
