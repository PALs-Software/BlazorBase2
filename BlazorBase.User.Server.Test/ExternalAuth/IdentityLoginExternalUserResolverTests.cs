using System.Security.Claims;
using BlazorBase.User.Server.ExternalAuth;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.ExternalAuth;

/// <summary>
/// Tests the framework's default <see cref="IdentityLoginExternalUserResolver{TUser}"/> in
/// isolation, and that <c>AddBlazorBaseUserServer</c> registers it only where a host has not
/// already claimed <see cref="IExternalUserResolver{TUser}"/> itself.
/// </summary>
public sealed class IdentityLoginExternalUserResolverTests
{
    private sealed class StubExternalUserResolver : IExternalUserResolver<TestUser>
    {
        public Task<ExternalUserResolution<TestUser>> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Never invoked - only its registered type is asserted on.");
    }

    private static ClaimsPrincipal BuildPrincipal(string? oid = null, string? sub = null, string? email = null)
    {
        var claims = new List<Claim>();

        if (oid is not null)
            claims.Add(new Claim("oid", oid));

        if (sub is not null)
            claims.Add(new Claim("sub", sub));

        if (email is not null)
            claims.Add(new Claim(ClaimTypes.Email, email));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "External"));
    }

    private static async Task<(ServiceProvider Services, UserManager<TestUser> Users, IdentityLoginExternalUserResolver<TestUser> Resolver)> CreateResolverAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<TestUser>().AddEntityFrameworkStores<TestUserDbContext>();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<TestUserDbContext>().Database.EnsureCreatedAsync();

        var users = provider.GetRequiredService<UserManager<TestUser>>();
        var resolver = new IdentityLoginExternalUserResolver<TestUser>(users, Options.Create(new ExternalAuthSettings()));

        return (provider, users, resolver);
    }

    private static async Task<TestUser> CreateUserAsync(UserManager<TestUser> users, string userName, string? linkedProviderKey = null)
    {
        var user = new TestUser { UserName = userName, Email = $"{userName}@example.test" };
        var result = await users.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));

        if (linkedProviderKey is not null)
        {
            var linkResult = await users.AddLoginAsync(user, new UserLoginInfo(ExternalAuthSettings.DefaultLoginProvider, linkedProviderKey, linkedProviderKey));
            Assert.True(linkResult.Succeeded, string.Join("; ", linkResult.Errors.Select(error => error.Description)));
        }

        return user;
    }

    [Fact]
    public async Task Accepts_when_the_oid_claim_matches_a_stored_Identity_login()
    {
        var (_, users, resolver) = await CreateResolverAsync();
        var user = await CreateUserAsync(users, "known-oid-user", linkedProviderKey: "subject-oid-1");

        var resolution = await resolver.ResolveAsync(BuildPrincipal(oid: "subject-oid-1"), CancellationToken.None);

        Assert.True(resolution.IsAccepted);
        Assert.Equal(user.Id, resolution.User!.Id);
    }

    [Fact]
    public async Task Uses_the_sub_claim_when_oid_is_absent()
    {
        var (_, users, resolver) = await CreateResolverAsync();
        var user = await CreateUserAsync(users, "known-sub-user", linkedProviderKey: "subject-sub-1");

        var resolution = await resolver.ResolveAsync(BuildPrincipal(sub: "subject-sub-1"), CancellationToken.None);

        Assert.True(resolution.IsAccepted);
        Assert.Equal(user.Id, resolution.User!.Id);
    }

    [Fact]
    public async Task Rejects_an_unrecognised_subject_even_when_its_email_matches_an_existing_account()
    {
        var (_, users, resolver) = await CreateResolverAsync();
        await CreateUserAsync(users, "victim-user");
        var victim = await users.FindByNameAsync("victim-user");

        var resolution = await resolver.ResolveAsync(BuildPrincipal(oid: "attacker-subject-never-linked", email: victim!.Email), CancellationToken.None);

        Assert.False(resolution.IsAccepted);
    }

    [Fact]
    public async Task Rejects_a_token_with_neither_an_oid_nor_a_sub_claim()
    {
        var (_, _, resolver) = await CreateResolverAsync();

        var resolution = await resolver.ResolveAsync(BuildPrincipal(email: "no-subject@example.test"), CancellationToken.None);

        Assert.False(resolution.IsAccepted);
    }

    [Fact]
    public void AddBlazorBaseUserServer_registers_the_default_resolver_when_a_host_registers_none()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options => options.RequiresAuthSessionEndpoint = false);

        var resolver = services.BuildServiceProvider().GetRequiredService<IExternalUserResolver<TestUser>>();

        Assert.IsType<IdentityLoginExternalUserResolver<TestUser>>(resolver);
    }

    [Fact]
    public void A_host_resolver_registered_before_AddBlazorBaseUserServer_still_wins()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IExternalUserResolver<TestUser>, StubExternalUserResolver>();
        services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options => options.RequiresAuthSessionEndpoint = false);

        var resolver = services.BuildServiceProvider().GetRequiredService<IExternalUserResolver<TestUser>>();

        Assert.IsType<StubExternalUserResolver>(resolver);
    }

    [Fact]
    public void A_host_resolver_registered_after_AddBlazorBaseUserServer_still_wins()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options => options.RequiresAuthSessionEndpoint = false);
        services.AddScoped<IExternalUserResolver<TestUser>, StubExternalUserResolver>();

        var resolver = services.BuildServiceProvider().GetRequiredService<IExternalUserResolver<TestUser>>();

        Assert.IsType<StubExternalUserResolver>(resolver);
    }
}
