using System.Net;
using BlazorBase.CRUD.Benchmarks.Entities;
using BlazorBase.CRUD.Endpoints;
using BlazorBase.CRUD.Extensions;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorBase.CRUD.Benchmarks.Infrastructure;

public sealed class BenchmarkWebHost : IAsyncDisposable
{
    private readonly WebApplication Application;

    private BenchmarkWebHost(WebApplication application, string baseAddress)
    {
        Application = application;
        BaseAddress = new Uri(baseAddress);
    }

    public Uri BaseAddress { get; }

    public static async Task<BenchmarkWebHost> StartAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BenchmarkWebHost).Assembly.GetName().Name,
            EnvironmentName = Environments.Production
        });

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.UseKestrel(o =>
        {
            o.AddServerHeader = false;
        });

        builder.Services.AddBlazorBaseCrud();
        builder.Services.AddBaseDbContext<BenchmarkDbContext>(o => o.UseSqlite(connectionString));
        builder.Services.AddBlazorBaseCrudServer<BenchmarkDbContext>(typeof(BenchmarkDbContext).Assembly);

        var app = builder.Build();

        MapBlazorBaseEndpoints(app);
        MapRawProductEndpoints(app);
        MapRawOrderEndpoints(app);

        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var baseAddress = addresses?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("Could not resolve benchmark host base address.");

        return new BenchmarkWebHost(app, baseAddress);
    }

    public async ValueTask DisposeAsync()
    {
        await Application.StopAsync();
        await Application.DisposeAsync();
    }

    private static void MapBlazorBaseEndpoints(WebApplication app)
    {
        app.MapBaseEndpoints<BenchProduct>("bench-products", o => o.RequireAuth = false);
        app.MapBaseEndpoints<BenchCategory>("bench-categories", o => o.RequireAuth = false);
        app.MapBaseEndpoints<BenchOrder>("bench-orders", o => o.RequireAuth = false);
        app.MapBaseEndpoints<BenchOrderItem>("bench-order-items", o => o.RequireAuth = false);
    }

    private static void MapRawProductEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/raw/products");

        group.MapGet("/{id:guid}", async (Guid id, BenchmarkDbContext ctx, CancellationToken ct) =>
        {
            var product = await ctx.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        group.MapGet("/count", async (BenchmarkDbContext ctx, CancellationToken ct) =>
            Results.Ok(await ctx.Products.CountAsync(ct)));

        group.MapPost("/query", async (RawListRequest request, BenchmarkDbContext ctx, CancellationToken ct) =>
        {
            IQueryable<BenchProduct> q = ctx.Products.AsNoTracking();

            if (request.OnlyActive == true)
                q = q.Where(p => p.IsActive);

            if (!string.IsNullOrEmpty(request.NameContains))
                q = q.Where(p => p.Name.Contains(request.NameContains));

            if (request.OrderByName == true)
                q = q.OrderBy(p => p.Name);

            var items = await q.Take(request.Take ?? 50).ToListAsync(ct);
            return Results.Ok(new RawListResponse<BenchProduct>(items, items.Count));
        });

        group.MapPost("/", async (BenchProduct product, BenchmarkDbContext ctx, CancellationToken ct) =>
        {
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync(ct);
            return Results.Created($"/api/raw/products/{product.Id}", product);
        });

        group.MapPatch("/{id:guid}", async (Guid id, RawPatch patch, BenchmarkDbContext ctx, CancellationToken ct) =>
        {
            var entity = await ctx.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (entity is null)
                return Results.NotFound();

            if (patch.Price.HasValue) entity.Price = patch.Price.Value;
            if (patch.Stock.HasValue) entity.Stock = patch.Stock.Value;
            if (patch.Name is not null) entity.Name = patch.Name;
            if (patch.Description is not null) entity.Description = patch.Description;
            if (patch.IsActive.HasValue) entity.IsActive = patch.IsActive.Value;

            await ctx.SaveChangesAsync(ct);
            return Results.Ok(entity);
        });

        group.MapDelete("/{id:guid}", async (Guid id, BenchmarkDbContext ctx, CancellationToken ct) =>
        {
            var entity = await ctx.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (entity is null)
                return Results.NotFound();
            ctx.Products.Remove(entity);
            await ctx.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static void MapRawOrderEndpoints(WebApplication app)
    {
        app.MapPost("/api/raw/orders/with-items", async (int take, BenchmarkDbContext ctx, CancellationToken ct) =>
        {
            var orders = await ctx.Orders.AsNoTracking()
                .Where(o => o.IsActive)
                .OrderBy(o => o.OrderDate)
                .Include(o => o.Items.Where(i => i.IsActive).OrderBy(i => i.DisplayOrder))
                .Take(take)
                .ToListAsync(ct);
            return Results.Ok(new RawListResponse<BenchOrder>(orders, orders.Count));
        });
    }
}

public sealed record RawListRequest(bool? OnlyActive, string? NameContains, bool? OrderByName, int? Take);

public sealed record RawListResponse<T>(IReadOnlyList<T> Items, int Count);

public sealed record RawPatch(decimal? Price, int? Stock, string? Name, string? Description, bool? IsActive);
