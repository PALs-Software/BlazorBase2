using System.Net.Http.Json;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Endpoints;
using BlazorBase.CRUD.Extensions;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.CRUD.Test.Endpoints;

/// <summary>
/// Proves that <see cref="IBaseEndpointConfigurator{TEntity}"/>, resolved from DI, is applied to
/// every registration of an entity's endpoints — the assembly-scanning overload, the typed
/// overload with no explicit <c>configure</c>, and the typed overload with one — and that in the
/// last case the configurator runs after <c>configure</c> and wins.
/// </summary>
public sealed class BaseEndpointMapperConfiguratorTests
{
    private sealed class AliceOnlyConfigurator : IBaseEndpointConfigurator<ConfiguratorTestEntity>
    {
        public void Configure(BaseEndpointOptions<ConfiguratorTestEntity> options) =>
            options.UserFilter = _ => entity => entity.Owner == "alice";
    }

    [Fact]
    public async Task A_DI_registered_configurator_scopes_scan_and_typed_registrations_and_overrides_an_explicit_configure()
    {
        var provider = new RecordingDataProvider();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddAuthentication(AlwaysAuthenticatedHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, AlwaysAuthenticatedHandler>(
                AlwaysAuthenticatedHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IBaseDataProvider<ConfiguratorTestEntity>>(provider);
        builder.Services.AddSingleton<IBaseEndpointConfigurator<ConfiguratorTestEntity>, AliceOnlyConfigurator>();
        RegisterNoOpProvidersForEveryOtherScannedEntity(builder.Services);

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapBlazorBaseCrudEndpoints(typeof(ConfiguratorTestEntity).Assembly);

        app.MapBlazorBaseCrudEndpoints<ConfiguratorTestEntity>("typed-entities");

        app.MapBlazorBaseCrudEndpoints<ConfiguratorTestEntity>(
            "typed-entities-explicit-configure",
            configure: options => options.UserFilter = _ => entity => entity.Owner == "bob");

        await app.StartAsync();

        using var client = app.GetTestClient();

        var scanOwners = await QueryOwnersAsync(client, "api/base/configurator-entities/query");
        var typedOwners = await QueryOwnersAsync(client, "api/base/typed-entities/query");
        var typedWithExplicitConfigureOwners = await QueryOwnersAsync(client, "api/base/typed-entities-explicit-configure/query");

        Assert.Equal(["alice"], scanOwners);
        Assert.Equal(["alice"], typedOwners);
        Assert.Equal(["alice"], typedWithExplicitConfigureOwners);
    }

    /// <summary>
    /// The test assembly carries several other [BaseCrud] entities (used by unrelated tests).
    /// <c>MapBlazorBaseCrudEndpoints(assembly)</c> maps every one of them, and route metadata
    /// inference fails for any whose <see cref="IBaseDataProvider{TModel}"/> is unregistered — even
    /// though none of their routes are ever called in this test.
    /// </summary>
    private static void RegisterNoOpProvidersForEveryOtherScannedEntity(IServiceCollection services)
    {
        foreach (var entityType in BaseServiceCollectionExtensions.FindBaseCrudTypes(typeof(ConfiguratorTestEntity).Assembly))
        {
            if (entityType == typeof(ConfiguratorTestEntity))
                continue;

            var providerInterface = typeof(IBaseDataProvider<>).MakeGenericType(entityType);
            var providerImplementation = typeof(NoOpDataProvider<>).MakeGenericType(entityType);
            services.AddSingleton(providerInterface, Activator.CreateInstance(providerImplementation)!);
        }
    }

    private static async Task<List<string>> QueryOwnersAsync(HttpClient client, string path)
    {
        var response = await client.PostAsJsonAsync(path, new BaseQuery());
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BaseQueryResult<ConfiguratorTestEntity>>();
        Assert.NotNull(result);

        return result.Items.Select(item => item.Owner).ToList();
    }
}
