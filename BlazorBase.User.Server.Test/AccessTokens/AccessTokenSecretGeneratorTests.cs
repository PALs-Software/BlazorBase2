using BlazorBase.User.Server.AccessTokens.Generation;
using BlazorBase.User.Server.AccessTokens;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.AccessTokens;

public class AccessTokenSecretGeneratorTests
{
    private static AccessTokenSecretGenerator CreateGenerator(string secretPrefix = "test_", int secretByteLength = 32) =>
        new(Options.Create(new AccessTokenSettings { SecretPrefix = secretPrefix, SecretByteLength = secretByteLength }));

    [Fact]
    public void A_generated_secret_carries_the_configured_prefix()
    {
        var generated = CreateGenerator("myapp_").Generate();

        Assert.StartsWith("myapp_", generated.Secret);
        Assert.StartsWith("myapp_", generated.Prefix);
    }

    [Fact]
    public void The_stored_prefix_is_a_leading_slice_of_the_secret()
    {
        var generated = CreateGenerator().Generate();

        Assert.StartsWith(generated.Prefix, generated.Secret);
        Assert.Equal("test_".Length + 8, generated.Prefix.Length);
    }

    [Fact]
    public void Two_secrets_never_repeat()
    {
        var generator = CreateGenerator();

        var secrets = Enumerable.Range(0, 100).Select(_ => generator.Generate().Secret).ToList();

        Assert.Equal(secrets.Count, secrets.Distinct().Count());
    }

    [Fact]
    public void A_secret_length_below_the_floor_is_raised_rather_than_honoured()
    {
        var weak = CreateGenerator(secretByteLength: 4).Generate();
        var normal = CreateGenerator(secretByteLength: 32).Generate();

        Assert.Equal(normal.Secret.Length, weak.Secret.Length);
    }

    [Fact]
    public void A_secret_matches_its_own_hash_and_nothing_else()
    {
        var generator = CreateGenerator();
        var generated = generator.Generate();
        var other = generator.Generate();

        Assert.True(generator.MatchesHash(generated.Secret, generated.Hash));
        Assert.False(generator.MatchesHash(other.Secret, generated.Hash));
    }

    [Fact]
    public void Hashing_is_deterministic()
    {
        var generator = CreateGenerator();

        Assert.Equal(generator.Hash("test_abcdefgh"), generator.Hash("test_abcdefgh"));
    }

    /// <summary>
    /// The Base64Url body can itself contain the underscore that ends the literal prefix, because
    /// Base64Url maps <c>/</c> to <c>_</c>. Extraction is positional for exactly that reason - an
    /// implementation that searched for the separator would cut in the wrong place here.
    /// </summary>
    [Fact]
    public void An_underscore_inside_the_body_does_not_move_the_prefix_boundary()
    {
        var generator = CreateGenerator();

        var prefix = generator.ExtractPrefix("test_ab_defghijklmno");

        Assert.Equal("test_ab_defgh", prefix);
    }

    [Fact]
    public void A_secret_shorter_than_the_prefix_is_returned_unchanged_rather_than_throwing()
    {
        var generator = CreateGenerator();

        Assert.Equal("test_", generator.ExtractPrefix("test_"));
        Assert.Equal(string.Empty, generator.ExtractPrefix(string.Empty));
    }

    [Fact]
    public void Extraction_round_trips_with_generation()
    {
        var generator = CreateGenerator("dpat_");
        var generated = generator.Generate();

        Assert.Equal(generated.Prefix, generator.ExtractPrefix(generated.Secret));
    }
}
