using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Endpoints;
using BlazorBase.CRUD.Extensions;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Endpoints;

/// <summary>
/// An entity whose key is not named <c>Id</c> is addressed by its key on every endpoint: the id of the route is
/// parsed to the key's type, the created location carries the key, and the scope check selects the key.
/// </summary>
public sealed class BaseEndpointMapperKeyTests
{
    public sealed class OpenKeyed
    {
        public Guid OpenKeyedId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task Patch_ParsesTheRouteIdToTheTypeOfTheKey()
    {
        var id = Guid.NewGuid();
        var provider = Substitute.For<IBaseDataProvider<OpenKeyed>>();
        provider.PatchAsync(Arg.Any<object>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new OpenKeyed { OpenKeyedId = id, Name = "Patched" });

        await using var app = await StartAsync(provider);
        using var client = app.GetTestClient();

        var response = await client.PatchAsJsonAsync($"api/base/keyed/{id}", new PatchModel { ChangedFields = new() { ["Name"] = "Patched" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await provider.Received(1).PatchAsync(id, Arg.Any<Dictionary<string, object?>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_ParsesTheRouteIdToTheTypeOfTheKey()
    {
        var id = Guid.NewGuid();
        var provider = Substitute.For<IBaseDataProvider<OpenKeyed>>();

        await using var app = await StartAsync(provider);
        using var client = app.GetTestClient();

        var response = await client.DeleteAsync($"api/base/keyed/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await provider.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ReturnsALocationWithTheKey()
    {
        var id = Guid.NewGuid();
        var provider = Substitute.For<IBaseDataProvider<OpenKeyed>>();
        provider.CreateAsync(Arg.Any<OpenKeyed>(), Arg.Any<CancellationToken>())
            .Returns(new OpenKeyed { OpenKeyedId = id, Name = "Created" });

        await using var app = await StartAsync(provider);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("api/base/keyed", new OpenKeyed { Name = "Created" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"/{id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task TheScopeCheck_SelectsTheKeyOfTheEntity()
    {
        var id = Guid.NewGuid();
        IEnumerable<string>? selected = null;
        var provider = Substitute.For<IBaseDataProvider<OpenKeyed>>();
        provider.GetByIdAsync(Arg.Any<object>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<Expression<Func<OpenKeyed, bool>>?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                selected = call.Arg<IEnumerable<string>?>();
                return Task.FromResult<OpenKeyed?>(new OpenKeyed { OpenKeyedId = id });
            });

        await using var app = await StartAsync(provider, options => options.UserFilter = _ => entity => entity.Name != "hidden");
        using var client = app.GetTestClient();

        var response = await client.DeleteAsync($"api/base/keyed/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([nameof(OpenKeyed.OpenKeyedId)], selected);
    }

    private static async Task<WebApplication> StartAsync(
        IBaseDataProvider<OpenKeyed> provider,
        Action<BaseEndpointOptions<OpenKeyed>>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddAuthentication(AlwaysAuthenticatedHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, AlwaysAuthenticatedHandler>(AlwaysAuthenticatedHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(provider);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapBlazorBaseCrudEndpoints<OpenKeyed>("keyed", configure);
        await app.StartAsync();
        return app;
    }
}
