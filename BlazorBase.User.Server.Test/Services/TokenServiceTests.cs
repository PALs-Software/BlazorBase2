using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using Microsoft.Extensions.Configuration;
using Xunit;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Server.Test.Services;

/// <summary>
/// Unit tests for <see cref="TokenService{TUser}"/> — the security-critical JWT issuing and
/// validation logic. No database is involved; everything is driven through <see cref="IConfiguration"/>.
/// </summary>
public sealed class TokenServiceTests
{
    private const string Secret = "blazorbase-test-signing-secret-key-0123456789-abcdef";
    private const string Issuer = "BlazorBase.Issuer";
    private const string Audience = "BlazorBase.Audience";

    private static IConfiguration BuildConfiguration(
        string? secret = Secret,
        string issuer = Issuer,
        string audience = Audience,
        int expirationMinutes = 60)
    {
        var values = new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = issuer,
            ["JwtSettings:Audience"] = audience,
            ["JwtSettings:ExpirationMinutes"] = expirationMinutes.ToString(),
        };

        if (secret is not null)
            values["JwtSettings:Secret"] = secret;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static TokenService<BaseUser> CreateService(
        string? secret = Secret,
        string issuer = Issuer,
        string audience = Audience,
        int expirationMinutes = 60)
        => new(BuildConfiguration(secret, issuer, audience, expirationMinutes), []);

    private static BaseUser SampleUser() => new()
    {
        Id = "user-1",
        Email = "ada@example.com",
        DisplayName = "Ada Lovelace",
        ThemePreference = "Dark",
        Language = "de",
    };

    [Fact]
    public async Task GenerateAccessToken_IncludesExpectedUserClaims()
    {
        var token = await CreateService().GenerateAccessToken(SampleUser(), []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("user-1", jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("ada@example.com", jwt.Claims.Single(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("Ada Lovelace", jwt.Claims.Single(c => c.Type == "displayName").Value);
        Assert.Equal("Dark", jwt.Claims.Single(c => c.Type == "themePreference").Value);
        Assert.Equal("de", jwt.Claims.Single(c => c.Type == "language").Value);
    }

    [Fact]
    public async Task GenerateAccessToken_IncludesIssuerAndAudience()
    {
        var token = await CreateService().GenerateAccessToken(SampleUser(), []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Contains(Audience, jwt.Audiences);
    }

    [Fact]
    public async Task GenerateAccessToken_EmitsOneRoleClaimPerRole()
    {
        var token = await CreateService().GenerateAccessToken(SampleUser(), ["Admin", "User"]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

        Assert.Equal(["Admin", "User"], roles);
    }

    [Fact]
    public async Task GenerateAccessToken_HonorsConfiguredExpiration()
    {
        var before = DateTime.UtcNow;
        var token = await CreateService(expirationMinutes: 30).GenerateAccessToken(SampleUser(), []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.InRange(jwt.ValidTo, before.AddMinutes(29), before.AddMinutes(31));
    }

    [Fact]
    public async Task GenerateAccessToken_ThrowsWhenSecretMissing()
    {
        var service = CreateService(secret: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAccessToken(SampleUser(), []));
    }

    [Fact]
    public void GenerateRefreshToken_ProducesUnique512BitBase64Tokens()
    {
        var service = CreateService();

        var first = service.GenerateRefreshToken();
        var second = service.GenerateRefreshToken();

        Assert.NotEqual(first, second);
        Assert.Equal(64, Convert.FromBase64String(first).Length);
        Assert.Equal(64, Convert.FromBase64String(second).Length);
    }

    [Fact]
    public async Task ValidateAccessToken_RoundTripsAValidToken()
    {
        var service = CreateService();
        var token = await service.GenerateAccessToken(SampleUser(), ["Admin"]);

        var principal = service.ValidateAccessToken(token);

        Assert.NotNull(principal);
        Assert.Equal("user-1", principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Admin", principal.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public async Task ValidateAccessToken_ReturnsNullForTamperedSignature()
    {
        var signedWithOtherKey = await CreateService(secret: "a-totally-different-signing-secret-key-9876543210-zyxwv")
            .GenerateAccessToken(SampleUser(), []);

        var principal = CreateService().ValidateAccessToken(signedWithOtherKey);

        Assert.Null(principal);
    }

    [Fact]
    public async Task ValidateAccessToken_ReturnsNullForWrongIssuer()
    {
        var foreignIssuerToken = await CreateService(issuer: "Evil.Issuer").GenerateAccessToken(SampleUser(), []);

        var principal = CreateService().ValidateAccessToken(foreignIssuerToken);

        Assert.Null(principal);
    }

    [Fact]
    public async Task ValidateAccessToken_ReturnsNullForWrongAudience()
    {
        var foreignAudienceToken = await CreateService(audience: "Evil.Audience").GenerateAccessToken(SampleUser(), []);

        var principal = CreateService().ValidateAccessToken(foreignAudienceToken);

        Assert.Null(principal);
    }

    [Fact]
    public void ValidateAccessToken_ReturnsNullForGarbage()
    {
        Assert.Null(CreateService().ValidateAccessToken("not-a-jwt"));
    }

    [Fact]
    public async Task ValidateAccessToken_ReturnsNullWhenSecretMissing()
    {
        var token = await CreateService().GenerateAccessToken(SampleUser(), []);

        Assert.Null(CreateService(secret: null).ValidateAccessToken(token));
    }

    [Fact]
    public async Task ValidateAccessToken_RejectsExpiredToken()
    {
        var expiredToken = await CreateService(expirationMinutes: -10).GenerateAccessToken(SampleUser(), []);

        var principal = CreateService().ValidateAccessToken(expiredToken);

        Assert.Null(principal);
    }

    [Fact]
    public async Task GenerateAccessToken_AppendsClaimsFromAugmentors()
    {
        var augmentor = new StubClaimsAugmentor([new Claim("tenant_id", "house-42")]);
        var service = new TokenService<BaseUser>(
            BuildConfiguration(),
            [augmentor]);

        var token = await service.GenerateAccessToken(SampleUser(), []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var tenantClaim = jwt.Claims.SingleOrDefault(c => c.Type == "tenant_id");
        Assert.NotNull(tenantClaim);
        Assert.Equal("house-42", tenantClaim!.Value);
    }

    private sealed class StubClaimsAugmentor(IEnumerable<Claim> claims) : IClaimsAugmentor<BaseUser>
    {
        #region Injects
        private readonly IEnumerable<Claim> Claims = claims;
        #endregion

        public Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(BaseUser user, IList<string> roles)
            => Task.FromResult(Claims);
    }
}
