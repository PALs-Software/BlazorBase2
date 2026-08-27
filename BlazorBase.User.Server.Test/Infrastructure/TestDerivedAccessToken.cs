using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// A host-defined TPH-derived <see cref="AccessToken"/> carrying one application-specific binding,
/// used to exercise <see cref="BlazorBase.User.Server.AccessTokens.IAccessTokenService.CreateAsync{TToken}"/>.
/// </summary>
public sealed class TestDerivedAccessToken : AccessToken
{
    public string? Binding { get; set; }
}
