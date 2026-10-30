using System.Net;
using System.Net.Http.Json;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.SavedFilters;
using BlazorBase.CRUD.SavedFilters.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.CRUD.Test.SavedFilters;

/// <summary>
/// The saved-filter endpoints end to end: the real HTTP store against the mapped endpoints and the EF repository on
/// SQLite, with two users.
/// </summary>
public sealed class ListFilterEndpointTests : IAsyncLifetime
{
    private const string ListKey = "Orders";
    private const string Route = "api/list-filters";

    private readonly SavedFilterTestDatabase Database = new();
    private WebApplication App = default!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddAuthentication(HeaderUserAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderUserAuthenticationHandler>(HeaderUserAuthenticationHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.IDbContextFactory<SavedFilterTestDbContext>>(Database);
        builder.Services.AddBlazorBaseListFilters<SavedFilterTestDbContext>();

        App = builder.Build();
        App.UseAuthentication();
        App.UseAuthorization();
        App.MapBlazorBaseListFilters();

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

    private HttpListFilterStore StoreFor(string user) => new(ClientFor(user));

    private static SavedListFilterInput Input(string name, string value = "x", Guid? id = null, bool asDefault = false) => new()
    {
        Id = id,
        Name = name,
        SetAsDefault = asDefault,
        Filter = new FilterGroupModel
        {
            Conditions = [new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Equals, Value = value }]
        }
    };

    [Fact]
    public async Task EveryEndpoint_RequiresAnAuthenticatedUser()
    {
        using var anonymous = ClientFor(null);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"?listKey={ListKey}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"?listKey={ListKey}", Input("A"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync($"default?listKey={ListKey}", new SetListFilterDefaultRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"{id}?listKey={ListKey}")).StatusCode);
    }

    [Fact]
    public async Task HttpStore_SavesLoadsRenamesDefaultsAndDeletes()
    {
        var store = StoreFor("alice");

        var saved = await store.SaveAsync(ListKey, Input("Pending", "p"));
        await store.SaveAsync(ListKey, Input("Done", "d"));
        var renamed = await store.SaveAsync(ListKey, Input("Open", "p", saved.Id, asDefault: true));

        var all = await store.GetAsync(ListKey);
        Assert.Equal(["Done", "Open"], all.Select(f => f.Name));
        Assert.Equal(saved.Id, renamed.Id);
        Assert.Equal(["Open"], all.Where(f => f.IsDefault).Select(f => f.Name));
        Assert.Equal(FilterOperator.Equals, all[1].Filter.Conditions[0].Operator);

        Assert.True(await store.SetDefaultAsync(ListKey, null));
        Assert.DoesNotContain(await store.GetAsync(ListKey), f => f.IsDefault);

        Assert.True(await store.DeleteAsync(ListKey, saved.Id));
        Assert.False(await store.DeleteAsync(ListKey, saved.Id));
        Assert.False(await store.SetDefaultAsync(ListKey, Guid.NewGuid()));
        Assert.Equal(["Done"], (await store.GetAsync(ListKey)).Select(f => f.Name));
    }

    [Fact]
    public async Task HttpStore_MapsServerRefusalsToExceptions()
    {
        var store = StoreFor("alice");
        var a = await store.SaveAsync(ListKey, Input("A"));
        await store.SaveAsync(ListKey, Input("B"));

        await Assert.ThrowsAsync<ListFilterNameConflictException>(() => store.SaveAsync(ListKey, Input("B", id: a.Id)));
        await Assert.ThrowsAsync<ListFilterValidationException>(() => store.SaveAsync(ListKey, Input(" ")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.SaveAsync(ListKey, Input("C", id: Guid.NewGuid())));
    }

    [Fact]
    public async Task AnotherUser_CannotSeeChangeDefaultOrDeleteAUsersFilters()
    {
        var alice = StoreFor("alice");
        var bob = StoreFor("bob");
        var secret = await alice.SaveAsync(ListKey, Input("Secret", "alice-only", asDefault: true));

        Assert.Empty(await bob.GetAsync(ListKey));
        Assert.False(await bob.DeleteAsync(ListKey, secret.Id));
        Assert.False(await bob.SetDefaultAsync(ListKey, secret.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => bob.SaveAsync(ListKey, Input("Takeover", "bob", secret.Id)));

        var stillThere = Assert.Single(await alice.GetAsync(ListKey));
        Assert.Equal("Secret", stillThere.Name);
        Assert.Equal("alice-only", stillThere.Filter.Conditions[0].Value);
        Assert.True(stillThere.IsDefault);
    }

    [Fact]
    public async Task TheUserComesFromTheAuthenticatedPrincipal_NotFromTheRequest()
    {
        var alice = StoreFor("alice");
        await alice.SaveAsync(ListKey, Input("Secret"));

        using var bobClient = ClientFor("bob");
        var response = await bobClient.GetAsync($"?listKey={ListKey}&userId=alice");
        var visible = await response.Content.ReadFromJsonAsync<List<SavedListFilterInfo>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(visible!);
    }

    [Fact]
    public async Task MissingOrOverlongListKey_IsABadRequest()
    {
        using var client = ClientFor("alice");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(String.Empty)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"?listKey={new string('k', ListFilterValidator.MaxListKeyLength + 1)}")).StatusCode);
    }
}
