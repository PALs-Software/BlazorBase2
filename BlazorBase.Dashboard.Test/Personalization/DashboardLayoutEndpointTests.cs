using System.Net;
using System.Net.Http.Json;
using BlazorBase.Dashboard.Server;
using BlazorBase.Dashboard.Test.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Dashboard.Test.Personalization;

/// <summary>
/// The dashboard layout endpoints end to end: the real HTTP store against the mapped endpoints and the EF repository on
/// SQLite, with two users.
/// </summary>
public sealed class DashboardLayoutEndpointTests : IAsyncLifetime
{
    private const string Key = "/";
    private const string Route = "api/dashboard-layouts";

    private readonly DashboardLayoutTestDatabase Database = new();
    private WebApplication App = default!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddAuthentication(HeaderUserAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderUserAuthenticationHandler>(HeaderUserAuthenticationHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IDbContextFactory<DashboardLayoutTestDbContext>>(Database);
        builder.Services.AddBlazorBaseDashboardLayouts<DashboardLayoutTestDbContext>();

        App = builder.Build();
        App.UseAuthentication();
        App.UseAuthorization();
        App.MapBlazorBaseDashboardLayouts();

        await App.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();
        Database.Dispose();
    }

    private HttpClient ClientFor(string? user)
    {
        var client = App.GetTestServer().CreateClient();
        client.BaseAddress = new Uri($"http://localhost/{Route}/");

        if (user is not null)
            client.DefaultRequestHeaders.Add(HeaderUserAuthenticationHandler.HeaderName, user);

        return client;
    }

    private HttpDashboardLayoutStore StoreFor(string user) => new(ClientFor(user));

    private static DashboardLayout Layout(params (string Id, bool Hidden)[] parts) => new()
    {
        Parts = [.. parts.Select((item, index) => new DashboardPartPreference { PartId = item.Id, Hidden = item.Hidden, Order = index })]
    };

    [Fact]
    public async Task EveryEndpoint_RequiresAnAuthenticatedUser()
    {
        using var anonymous = ClientFor(null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"?dashboardKey={Key}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync($"?dashboardKey={Key}", Layout(("a", false)))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"?dashboardKey={Key}")).StatusCode);
    }

    [Fact]
    public async Task HttpStore_SavesLoadsAndResets()
    {
        var store = StoreFor("alice");

        Assert.True((await store.GetAsync(Key)).IsDefault);

        await store.SaveAsync(Key, Layout(("b", true), ("a", false)));

        var read = await store.GetAsync(Key);
        Assert.Equal(["b", "a"], read.Parts.Select(part => part.PartId));
        Assert.True(read.Parts[0].Hidden);

        await store.ResetAsync(Key);
        await store.ResetAsync(Key);

        Assert.True((await store.GetAsync(Key)).IsDefault);
    }

    [Fact]
    public async Task HttpStore_MapsServerRefusalsToValidationExceptions()
    {
        var store = StoreFor("alice");

        await Assert.ThrowsAsync<DashboardLayoutValidationException>(() => store.SaveAsync(Key, Layout([.. Enumerable.Range(0, 65).Select(index => ($"p{index}", false))])));
        await Assert.ThrowsAsync<DashboardLayoutValidationException>(() => store.SaveAsync(Key, Layout(("a", false), ("a", false))));
        await Assert.ThrowsAsync<DashboardLayoutValidationException>(() => store.SaveAsync(Key, Layout((new string('p', 101), false))));
    }

    [Fact]
    public async Task AnotherUser_CannotSeeChangeOrDeleteALayout()
    {
        var alice = StoreFor("alice");
        var bob = StoreFor("bob");
        await alice.SaveAsync(Key, Layout(("secret", true)));

        Assert.True((await bob.GetAsync(Key)).IsDefault);

        await bob.SaveAsync(Key, Layout(("bobs", false)));
        await bob.ResetAsync(Key);

        var untouched = Assert.Single((await alice.GetAsync(Key)).Parts);
        Assert.Equal("secret", untouched.PartId);
        Assert.True(untouched.Hidden);
    }

    [Fact]
    public async Task TheUserComesFromTheAuthenticatedPrincipal_NotFromTheRequest()
    {
        await StoreFor("alice").SaveAsync(Key, Layout(("secret", true)));

        using var bobClient = ClientFor("bob");
        var response = await bobClient.GetAsync($"?dashboardKey={Key}&userId=alice");
        var visible = await response.Content.ReadFromJsonAsync<DashboardLayout>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(visible!.IsDefault);
    }

    [Fact]
    public async Task MissingOrOverlongKey_IsABadRequest_OnEveryVerb()
    {
        using var client = ClientFor("alice");
        var tooLong = new string('k', DashboardLayoutValidator.MaxKeyLength + 1);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(String.Empty)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"?dashboardKey={tooLong}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(String.Empty, Layout(("a", false)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync(String.Empty)).StatusCode);
    }

    [Fact]
    public async Task AKeyWithSlashesAndSpecialCharacters_RoundTripsThroughTheQueryString()
    {
        var store = StoreFor("alice");
        const string key = "/reports/year 2026?x=1&y=2";

        await store.SaveAsync(key, Layout(("a", true)));

        Assert.True(Assert.Single((await store.GetAsync(key)).Parts).Hidden);
        Assert.True((await store.GetAsync(Key)).IsDefault);
    }
}
