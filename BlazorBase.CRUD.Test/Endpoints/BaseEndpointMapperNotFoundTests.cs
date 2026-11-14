using System.Net;
using System.Net.Http.Json;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Extensions;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.CRUD.Test.Endpoints;

/// <summary>
/// Proves that PATCH and DELETE of a row that does not exist, or that the caller's query cannot see
/// because of an EF Core global query filter, answer <c>404 Not Found</c> (not <c>500</c>) and leave
/// the stored data untouched.
/// </summary>
public sealed class BaseEndpointMapperNotFoundTests
{
    private const string CallerTenant = "caller";
    private const string OtherTenant = "other";
    private const string OriginalName = "original";
    private const string Route = "api/base/hidden-rows";

    private sealed class TestHost(
        WebApplication application,
        HttpClient client,
        SqliteConnection connection,
        DbContextOptions<HiddenRowTestDbContext> options
    ) : IAsyncDisposable
    {
        #region Injects
        private readonly WebApplication Application = application;
        private readonly SqliteConnection Connection = connection;
        private readonly DbContextOptions<HiddenRowTestDbContext> Options = options;
        #endregion

        public HttpClient Client { get; } = client;

        public async Task<Guid> SeedAsync(string tenant)
        {
            await using var context = new HiddenRowTestDbContext(Options, new HiddenRowTenant(tenant));
            var entity = new HiddenRowTestEntity { Id = Guid.NewGuid(), Name = OriginalName, Tenant = tenant };
            context.Entities.Add(entity);
            await context.SaveChangesAsync();
            return entity.Id;
        }

        public async Task<List<HiddenRowTestEntity>> ReadAllRowsAsync()
        {
            await using var context = new HiddenRowTestDbContext(Options, new HiddenRowTenant(OtherTenant));
            return await context.Entities.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Application.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private static async Task<TestHost> CreateHostAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HiddenRowTestDbContext>().UseSqlite(connection).Options;

        await using (var schemaContext = new HiddenRowTestDbContext(options, new HiddenRowTenant(CallerTenant)))
            await schemaContext.Database.EnsureCreatedAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddAuthentication(AlwaysAuthenticatedHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, AlwaysAuthenticatedHandler>(
                AlwaysAuthenticatedHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(new HiddenRowTenant(CallerTenant));
        builder.Services.AddDbContext<HiddenRowTestDbContext>(contextOptions => contextOptions.UseSqlite(connection));
        builder.Services.AddScoped<IBaseDataProvider<HiddenRowTestEntity>>(services =>
            new DbContextBaseDataProvider<HiddenRowTestEntity>(
                services.GetRequiredService<HiddenRowTestDbContext>(),
                []));

        var application = builder.Build();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapBlazorBaseCrudEndpoints<HiddenRowTestEntity>("hidden-rows");
        await application.StartAsync();

        return new TestHost(application, application.GetTestClient(), connection, options);
    }

    private static Task<HttpResponseMessage> PatchNameAsync(HttpClient client, Guid id)
        => client.PatchAsJsonAsync($"{Route}/{id}", new PatchModel
        {
            ChangedFields = new Dictionary<string, object?> { ["Name"] = "changed" }
        });

    [Fact]
    public async Task Patch_of_a_non_existing_id_answers_404_and_creates_nothing()
    {
        await using var host = await CreateHostAsync();

        var response = await PatchNameAsync(host.Client, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await host.ReadAllRowsAsync());
    }

    [Fact]
    public async Task Delete_of_a_non_existing_id_answers_404()
    {
        await using var host = await CreateHostAsync();
        var existingId = await host.SeedAsync(CallerTenant);

        var response = await host.Client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var remaining = Assert.Single(await host.ReadAllRowsAsync());
        Assert.Equal(existingId, remaining.Id);
    }

    [Fact]
    public async Task Patch_of_a_row_hidden_by_a_global_query_filter_answers_404_and_leaves_the_row_unchanged()
    {
        await using var host = await CreateHostAsync();
        var hiddenId = await host.SeedAsync(OtherTenant);

        var response = await PatchNameAsync(host.Client, hiddenId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stored = Assert.Single(await host.ReadAllRowsAsync());
        Assert.Equal(OriginalName, stored.Name);
        Assert.Equal(OtherTenant, stored.Tenant);
    }

    [Fact]
    public async Task Delete_of_a_row_hidden_by_a_global_query_filter_answers_404_and_keeps_the_row()
    {
        await using var host = await CreateHostAsync();
        var hiddenId = await host.SeedAsync(OtherTenant);

        var response = await host.Client.DeleteAsync($"{Route}/{hiddenId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stored = Assert.Single(await host.ReadAllRowsAsync());
        Assert.Equal(hiddenId, stored.Id);
    }

    [Fact]
    public async Task Patch_of_a_visible_row_still_succeeds()
    {
        await using var host = await CreateHostAsync();
        var visibleId = await host.SeedAsync(CallerTenant);

        var response = await PatchNameAsync(host.Client, visibleId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = Assert.Single(await host.ReadAllRowsAsync());
        Assert.Equal("changed", stored.Name);
    }

    [Fact]
    public async Task Delete_of_a_visible_row_still_succeeds()
    {
        await using var host = await CreateHostAsync();
        var visibleId = await host.SeedAsync(CallerTenant);

        var response = await host.Client.DeleteAsync($"{Route}/{visibleId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await host.ReadAllRowsAsync());
    }
}
