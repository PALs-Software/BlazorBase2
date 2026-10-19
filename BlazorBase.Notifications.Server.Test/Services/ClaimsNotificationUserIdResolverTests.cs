using System.Security.Claims;
using BlazorBase.Notifications.Server.Services;
using Xunit;

namespace BlazorBase.Notifications.Server.Test.Services;

public sealed class ClaimsNotificationUserIdResolverTests
{
    private static ClaimsPrincipal PrincipalWithNameIdentifier(string? value)
    {
        var claims = value is null ? [] : new[] { new Claim(ClaimTypes.NameIdentifier, value) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    [Fact]
    public async Task ResolveUserIdAsync_ReturnsTheParsedGuid_WhenTheNameIdentifierClaimIsAValidGuid()
    {
        var resolver = new ClaimsNotificationUserIdResolver();
        var userId = Guid.NewGuid();

        var resolved = await resolver.ResolveUserIdAsync(PrincipalWithNameIdentifier(userId.ToString()), CancellationToken.None);

        Assert.Equal(userId, resolved);
    }

    [Fact]
    public async Task ResolveUserIdAsync_ReturnsNull_WhenTheNameIdentifierClaimIsMissing()
    {
        var resolver = new ClaimsNotificationUserIdResolver();

        var resolved = await resolver.ResolveUserIdAsync(PrincipalWithNameIdentifier(null), CancellationToken.None);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveUserIdAsync_ReturnsNull_WhenTheNameIdentifierClaimIsNotAGuid()
    {
        var resolver = new ClaimsNotificationUserIdResolver();

        var resolved = await resolver.ResolveUserIdAsync(PrincipalWithNameIdentifier("not-a-guid"), CancellationToken.None);

        Assert.Null(resolved);
    }
}
