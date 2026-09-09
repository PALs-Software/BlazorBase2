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

    private static readonly SymmetricSecurityKey ForeignSigningKey = new(
    [
        32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17,
        16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1,
    ]);

    private static readonly SigningCredentials ForeignSigningCredentials = new(ForeignSigningKey, SecurityAlgorithms.HmacSha256);

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

    private static string CreateToken(string tenantId, string? scope = null, string? appRole = null) =>
        CreateTokenWithIssuer(tenantId, Authority(tenantId), scope, appRole);

    private static string CreateTokenWithIssuer(string tenantId, string issuer, string? scope = null, string? appRole = null)
    {
        var claims = new Dictionary<string, object> { ["tid"] = tenantId };

        if (scope is not null)
            claims["scp"] = scope;

        if (appRole is not null)
            claims["roles"] = new[] { appRole };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = Audience,
            Claims = claims,
            SigningCredentials = SigningCredentials,
            Expires = DateTime.UtcNow.AddMinutes(5),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string CreateCustomToken(
        string tenantId,
        string? scope,
        string audience,
        DateTime expires,
        SigningCredentials signingCredentials,
        DateTime? notBefore = null)
    {
        var claims = new Dictionary<string, object> { ["tid"] = tenantId };

        if (scope is not null)
            claims["scp"] = scope;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Authority(tenantId),
            Audience = audience,
            Claims = claims,
            SigningCredentials = signingCredentials,
            Expires = expires,
            NotBefore = notBefore ?? DateTime.UtcNow,
            IssuedAt = notBefore ?? DateTime.UtcNow,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string TamperSignature(string token)
    {
        var segments = token.Split('.');
        var signature = segments[^1];
        var lastCharacter = signature[^1];
        segments[^1] = signature[..^1] + (lastCharacter == 'A' ? 'B' : 'A');

        return string.Join('.', segments);
    }

    private static string CreateUnsignedToken(string tenantId, string scope, string audience)
    {
        var issuedAt = EpochTime.GetIntDate(DateTime.UtcNow);

        var header = """{"alg":"none","typ":"JWT"}""";
        var payload = $$"""
            {"tid":"{{tenantId}}","scp":"{{scope}}","aud":"{{audience}}","iss":"{{Authority(tenantId)}}","exp":{{issuedAt + 300}},"nbf":{{issuedAt}},"iat":{{issuedAt}}}
            """;

        return $"{Base64UrlEncoder.Encode(header)}.{Base64UrlEncoder.Encode(payload)}.";
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

    private static ExternalAuthSettings BuildSettings(string authority, IList<string> allowedTenantIds) =>
        new()
        {
            Authority = authority,
            Audiences = [Audience],
            AllowedTenantIds = allowedTenantIds,
            RequiredScopes = [],
            RequiredAppRoles = [],
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

    [Fact]
    public async Task A_tenant_templated_issuer_with_an_empty_AllowedTenantIds_is_rejected()
    {
        const string templatedAuthority = "https://contoso.b2clogin.com/{tenantid}/v2.0";
        var settings = BuildSettings(templatedAuthority, allowedTenantIds: []);
        var validator = CreateValidator(settings);
        var token = CreateTokenWithIssuer(AllowedTenantId, templatedAuthority);

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.Null(principal);
    }

    [Fact]
    public async Task A_tenant_templated_issuer_is_accepted_once_the_tid_is_in_a_non_empty_AllowedTenantIds()
    {
        const string templatedAuthority = "https://contoso.b2clogin.com/{tenantid}/v2.0";
        var settings = BuildSettings(templatedAuthority, allowedTenantIds: [AllowedTenantId]);
        var validator = CreateValidator(settings);
        var token = CreateTokenWithIssuer(AllowedTenantId, templatedAuthority);

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.NotNull(principal);
    }

    [Fact]
    public async Task A_fixed_non_templated_issuer_with_an_empty_AllowedTenantIds_is_still_accepted()
    {
        var fixedAuthority = Authority(AllowedTenantId);
        var settings = BuildSettings(fixedAuthority, allowedTenantIds: []);
        var validator = CreateValidator(settings);
        var token = CreateTokenWithIssuer(AllowedTenantId, fixedAuthority);

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.NotNull(principal);
    }

    [Fact]
    public async Task A_token_whose_signature_was_tampered_with_is_rejected()
    {
        var settings = BuildMultiTenantSettings();
        var validator = CreateValidator(settings);
        var token = CreateToken(AllowedTenantId, scope: "access_as_user");

        var principal = await validator.ValidateAsync(TamperSignature(token), CancellationToken.None);

        Assert.Null(principal);
    }

    [Fact]
    public async Task A_token_signed_with_a_different_key_is_rejected()
    {
        var settings = BuildMultiTenantSettings();
        var validator = CreateValidator(settings);
        var token = CreateCustomToken(AllowedTenantId, scope: "access_as_user", audience: Audience, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: ForeignSigningCredentials);

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.Null(principal);
    }

    [Fact]
    public async Task A_token_for_a_different_audience_is_rejected()
    {
        var settings = BuildMultiTenantSettings();
        var validator = CreateValidator(settings);
        var token = CreateCustomToken(AllowedTenantId, scope: "access_as_user", audience: "api://different-audience", expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: SigningCredentials);

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.Null(principal);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        var settings = BuildMultiTenantSettings();
        var validator = CreateValidator(settings);
        var expires = DateTime.UtcNow.AddMinutes(-10);
        var token = CreateCustomToken(AllowedTenantId, scope: "access_as_user", audience: Audience, expires: expires, signingCredentials: SigningCredentials, notBefore: expires.AddMinutes(-5));

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.Null(principal);
    }

    [Fact]
    public async Task An_unsigned_token_is_rejected()
    {
        var settings = BuildMultiTenantSettings();
        var validator = CreateValidator(settings);
        var token = CreateUnsignedToken(AllowedTenantId, scope: "access_as_user", audience: Audience);

        var principal = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.Null(principal);
    }

    private sealed class StubExternalTenantAllowList(bool allowed) : IExternalTenantAllowList
    {
        public Task<bool> IsAllowedAsync(string tenantId, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }
}
