using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.OAuth;

/// <summary>
/// <see cref="OAuthServerOptionsValidator"/> fails a misconfigured server at startup, and the host
/// refuses to start without the access-token subsystem the OAuth tokens are built on.
/// </summary>
public sealed class OAuthServerOptionsValidatorTests
{
    private static OAuthServerOptions ValidOptions() => new()
    {
        Scopes = new Dictionary<string, string> { ["mcp"] = "Use the tools" },
    };

    private static ValidateOptionsResult Validate(Action<OAuthServerOptions> configure)
    {
        var options = ValidOptions();
        configure(options);
        return new OAuthServerOptionsValidator().Validate(null, options);
    }

    [Fact]
    public void The_defaults_with_a_scope_are_valid()
    {
        Assert.True(new OAuthServerOptionsValidator().Validate(null, ValidOptions()).Succeeded);
    }

    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("https://app.example.com/base/")]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1")]
    public void An_https_or_loopback_issuer_is_accepted(string issuerUri)
    {
        Assert.True(Validate(options => options.IssuerUri = issuerUri).Succeeded);
    }

    [Theory]
    [InlineData("http://app.example.com")]
    [InlineData("app.example.com")]
    [InlineData("https://app.example.com/?tenant=1")]
    public void An_insecure_or_malformed_issuer_is_rejected(string issuerUri)
    {
        Assert.True(Validate(options => options.IssuerUri = issuerUri).Failed);
    }

    [Theory]
    [InlineData("oauth/consent")]
    [InlineData("//evil.example/consent")]
    [InlineData("/consent?x=1")]
    public void A_non_local_consent_page_path_is_rejected(string consentPagePath)
    {
        Assert.True(Validate(options => options.ConsentPagePath = consentPagePath).Failed);
    }

    [Fact]
    public void An_empty_endpoint_base_path_is_rejected()
    {
        Assert.True(Validate(options => options.EndpointBasePath = "/").Failed);
    }

    [Fact]
    public void An_empty_scope_list_and_an_invalid_scope_name_are_rejected()
    {
        Assert.True(Validate(options => options.Scopes.Clear()).Failed);
        Assert.True(Validate(options => options.Scopes["read write"] = "Has a space").Failed);
    }

    [Fact]
    public void Non_positive_lifetimes_and_a_negative_grace_period_are_rejected()
    {
        Assert.True(Validate(options => options.AccessTokenLifetime = TimeSpan.Zero).Failed);
        Assert.True(Validate(options => options.AuthorizationCodeLifetime = TimeSpan.FromSeconds(-1)).Failed);
        Assert.True(Validate(options => options.RefreshTokenReuseGracePeriod = TimeSpan.FromSeconds(-1)).Failed);
        Assert.True(Validate(options => options.RefreshTokenReuseGracePeriod = TimeSpan.Zero).Succeeded);
    }

    [Theory]
    [InlineData("javascript")]
    [InlineData("http")]
    [InlineData("not a scheme")]
    public void A_dangerous_or_invalid_additional_redirect_scheme_is_rejected(string scheme)
    {
        Assert.True(Validate(options => options.AdditionalRedirectUriSchemes.Add(scheme)).Failed);
    }

    [Fact]
    public async Task An_invalid_configuration_fails_the_host_at_startup()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(() =>
            OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["IssuerUri"] = "http://app.example.com" }));
    }

    [Fact]
    public async Task The_host_refuses_to_start_without_the_access_token_subsystem()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => OAuthTestHost.StartAsync(registerAccessTokens: false));

        Assert.Contains("AddBlazorBaseAccessTokens", exception.Message);
    }
}
