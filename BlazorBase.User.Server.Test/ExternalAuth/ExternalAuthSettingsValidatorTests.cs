using BlazorBase.User.Server.ExternalAuth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.ExternalAuth;

/// <summary>
/// Drives <see cref="ExternalAuthSettingsValidator"/> against a real DI container, since it
/// resolves <see cref="IExternalTenantAllowList"/> from a scope to decide whether the static
/// <see cref="ExternalAuthSettings.AllowedTenantIds"/> requirement still applies.
/// </summary>
public sealed class ExternalAuthSettingsValidatorTests
{
    private const string Audience = "api://test-audience";

    private static ExternalAuthSettings BuildMultiTenantSettings(IList<string>? allowedTenantIds = null) => new()
    {
        Authority = "https://login.microsoftonline.com/common/v2.0",
        Audiences = [Audience],
        AllowedTenantIds = allowedTenantIds ?? [],
    };

    private static ExternalAuthSettingsValidator CreateValidator(IExternalTenantAllowList allowList)
    {
        var services = new ServiceCollection();
        services.AddSingleton(allowList);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new ExternalAuthSettingsValidator(scopeFactory);
    }

    [Fact]
    public void A_multi_tenant_authority_configured_with_an_empty_AllowedTenantIds_fails_at_startup_with_the_default_allow_list()
    {
        var settings = BuildMultiTenantSettings();
        var validator = CreateValidator(new DefaultExternalTenantAllowList(Options.Create(settings)));

        var result = validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("AllowedTenantIds", result.FailureMessage);
    }

    [Fact]
    public void A_multi_tenant_authority_configured_with_an_empty_AllowedTenantIds_succeeds_at_startup_with_a_custom_allow_list()
    {
        var settings = BuildMultiTenantSettings();
        var validator = CreateValidator(new StubExternalTenantAllowList());

        var result = validator.Validate(null, settings);

        Assert.False(result.Failed);
    }

    private sealed class StubExternalTenantAllowList : IExternalTenantAllowList
    {
        public Task<bool> IsAllowedAsync(string tenantId, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
