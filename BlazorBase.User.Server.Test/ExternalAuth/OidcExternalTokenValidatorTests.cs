using BlazorBase.User.Server.ExternalAuth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace BlazorBase.User.Server.Test.ExternalAuth;

/// <summary>
/// Drives <see cref="OidcExternalTokenValidator"/> with a locally minted token signed by a test
/// key and a stubbed <see cref="StaticConfigurationManager{T}"/> - no network access, but the same
/// signature/issuer/audience/scope checks the real validator performs against a real authority.
/// </summary>
public sealed class OidcExternalTokenValidatorTests
{
    private const string Audience = "api://test-audience";
    private const string AllowedTenantId = "11111111-1111-1111-1111-111111111111";
    private const string OtherTenantId = "22222222-2222-2222-2222-222222222222";

    private static readonly SymmetricSecurityKey SigningKey = new(
    [
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
    ]);

    private static readonly SigningCredentials SigningCredentials = new(SigningKey, SecurityAlgorithms.HmacSha256);

    private static string Authority(string tenantId) => $"https://login.microsoftonline.com/{tenantId}/v2.0";

    private static OidcExternalTokenValidator CreateValidator(ExternalAuthSettings settings, IExternalTenantAllowList? allowList = null)
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = settings.Authority };
        configuration.SigningKeys.Add(SigningKey);

        var configurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        var scopeFactory = BuildScopeFactory(allowList ?? new DefaultExternalTenantAllowList(Options.Create(settings)));

        return new OidcExternalTokenValidator(
            Options.Create(settings),
            NullLogger<OidcExternalTokenValidator>.Instance,
            scopeFactory,
            configurationManager);
    }

    private static IServiceScopeFactory BuildScopeFactory(IExternalTenantAllowList allowList)
    {
        var services = new ServiceCollection();
        services.AddSingleton(allowList);

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static string CreateToken(string tenantId, string? scope = null, string? appRole = null)
    {
        var claims = new Dictionary<string, object> { ["tid"] = tenantId };

        if (scope is not null)
            claims["scp"] = scope;

        if (appRole is not null)
            claims["roles"] = new[] { appRole };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Authority(tenantId),
            Audience = Audience,
            Claims = claims,
            SigningCredentials = SigningCredentials,
            Expires = DateTime.UtcNow.AddMinutes(5),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static ExternalAuthSettings BuildMultiTenantSettings(
        IList<string>? allowedTenantIds = null,
        IList<string>? requiredScopes = null,
        IList<string>? requiredAppRoles = null) =>
        new()
        {
            Authority = "https://login.microsoftonline.com/common/v2.0",
            Audiences = [Audience],
            AllowedTenantIds = allowedTenantIds ?? [AllowedTenantId],
            RequiredScopes = requiredScopes ?? ["access_as_user"],
            RequiredAppRoles = requiredAppRoles ?? [],
        };

    [Fact]
    public async Task A_token_from_an_allowed_tenant_carrying_a_required_scope_is_accepted()
    {
        var settings = BuildMultiTenantSettings();
        var validator = CreateValidator(settings);
        var token = CreateToken(AllowedTenantId, scope: "access_as_user");

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.NotNull(principal);
        Assert.Equal(AllowedTenantId, principal!.FindFirst("tid")?.Value);
    }

    [Fact]
    public async Task A_token_whose_tid_is_outside_AllowedTenantIds_is_rejected()
    {
        var settings = BuildMultiTenantSettings();
        var validator = CreateValidator(settings);
        var token = CreateToken(OtherTenantId, scope: "access_as_user");

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.Null(principal);
    }

    [Fact]
    public async Task A_valid_signature_token_with_neither_a_required_scope_nor_app_role_is_rejected()
    {
        var settings = BuildMultiTenantSettings();
        var validator = CreateValidator(settings);
        var token = CreateToken(AllowedTenantId);

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.Null(principal);
    }

    [Fact]
    public async Task A_token_carrying_a_required_app_role_instead_of_a_scope_is_accepted()
    {
        var settings = BuildMultiTenantSettings(requiredScopes: [], requiredAppRoles: ["Api.Access"]);
        var validator = CreateValidator(settings);
        var token = CreateToken(AllowedTenantId, appRole: "Api.Access");

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.NotNull(principal);
    }

    [Fact]
    public async Task A_token_whose_tid_is_outside_AllowedTenantIds_is_accepted_by_a_host_supplied_allow_list()
    {
        var settings = BuildMultiTenantSettings(allowedTenantIds: []);
        var validator = CreateValidator(settings, new StubExternalTenantAllowList(allowed: true));
        var token = CreateToken(OtherTenantId, scope: "access_as_user");

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.NotNull(principal);
    }

    private sealed class StubExternalTenantAllowList(bool allowed) : IExternalTenantAllowList
    {
        public Task<bool> IsAllowedAsync(string tenantId, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }
}
