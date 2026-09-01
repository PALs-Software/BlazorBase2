using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlazorBase.User.Server.Test.Services;

/// <summary>
/// Unit tests for <see cref="TokenService{TUser}"/> — the security-critical JWT issuing and
/// validation logic. No database is involved: <see cref="IUserClaimsPrincipalFactory{TUser}"/> is a
/// hand-written fake rather than the real <see cref="BlazorBaseUserClaimsPrincipalFactory{TUser}"/>,
/// which is instead exercised end-to-end (real Identity stack, real store) by
/// <c>AuthControllerTests</c> and <c>ExternalAuthExchangeEndpointTests</c>.
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
        int expirationMinutes = 60,
        IList<string>? roles = null)
        => new(BuildConfiguration(secret, issuer, audience, expirationMinutes), new FakeClaimsPrincipalFactory(roles ?? []));

    private static BaseUser SampleUser() => new()
    {
        Id = "user-1",
        Email = "ada@example.com",
        DisplayName = "Ada Lovelace",
        ThemePreference = "Dark",
        Language = "de",
        SecurityStamp = "stamp-abc123",
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
    public async Task GenerateAccessToken_IncludesSecurityStampClaim()
    {
        var token = await CreateService().GenerateAccessToken(SampleUser(), []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("stamp-abc123", jwt.Claims.Single(c => c.Type == "AspNet.Identity.SecurityStamp").Value);
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
        var token = await CreateService(roles: ["Admin", "User"]).GenerateAccessToken(SampleUser(), []);

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
        var service = CreateService(roles: ["Admin"]);
        var token = await service.GenerateAccessToken(SampleUser(), []);

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

    /// <summary>
    /// Stands in for <see cref="BlazorBaseUserClaimsPrincipalFactory{TUser}"/>, which needs a real
    /// <see cref="UserManager{TUser}"/>-backed store to run and is therefore exercised separately -
    /// see the type-level remarks. Reproduces the same claim set so <see cref="TokenService{TUser}"/>
    /// can be tested as a pure claims-to-JWT step.
    /// </summary>
    private sealed class FakeClaimsPrincipalFactory(IList<string> roles) : IUserClaimsPrincipalFactory<BaseUser>
    {
        #region Injects
        private readonly IList<string> Roles = roles;
        #endregion

        public Task<ClaimsPrincipal> CreateAsync(BaseUser user)
        {
            var identity = new ClaimsIdentity("Test", ClaimTypes.Name, ClaimTypes.Role);

            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email ?? string.Empty));
            identity.AddClaim(new Claim("displayName", user.DisplayName));
            identity.AddClaim(new Claim("themePreference", user.ThemePreference));
            identity.AddClaim(new Claim("language", user.Language));

            if (!string.IsNullOrEmpty(user.SecurityStamp))
                identity.AddClaim(new Claim("AspNet.Identity.SecurityStamp", user.SecurityStamp));

            foreach (var role in Roles)
                identity.AddClaim(new Claim(ClaimTypes.Role, role));

            return Task.FromResult(new ClaimsPrincipal(identity));
        }
    }
}
