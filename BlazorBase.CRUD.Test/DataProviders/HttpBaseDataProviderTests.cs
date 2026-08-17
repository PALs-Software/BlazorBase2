using System.Net;
using System.Net.Http.Json;
using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.DataProviders;

public class HttpBaseDataProviderTests
{
    private const string BaseAddress = "http://localhost/api/base/products/";

    [Fact]
    public async Task GetListAsync_PostsQueryToQueryEndpoint()
    {
        var (provider, handler) = CreateProvider(_ =>
            Json(new BaseQueryResult<TestProduct> { Items = [new TestProduct { Name = "P" }], TotalCount = 1 }));

        var result = await provider.GetListAsync(new BaseQuery());

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("P", Assert.Single(result.Items).Name);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("/api/base/products/query", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("take", handler.LastRequestBody);
    }

    [Fact]
    public async Task GetByIdAsync_GetsEntityById()
    {
        var id = Guid.NewGuid();
        var (provider, handler) = CreateProvider(_ => Json(new TestProduct { Name = "P" }));

        var result = await provider.GetByIdAsync(id);

        Assert.Equal("P", result!.Name);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal($"/api/base/products/{id}", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_WithSelect_AppendsSelectQueryParameter()
    {
        var (provider, handler) = CreateProvider(_ => Json(new TestProduct { Name = "P" }));

        await provider.GetByIdAsync(Guid.NewGuid(), select: ["Name", "Price"]);

        Assert.Contains("select=Name%2CPrice", handler.LastRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (provider, _) = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await provider.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCountAsync_GetsCountEndpoint()
    {
        var (provider, handler) = CreateProvider(_ => Json(42));

        var count = await provider.GetCountAsync();

        Assert.Equal(42, count);
        Assert.Equal("/api/base/products/count", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CreateAsync_PostsModelToCollectionEndpoint()
    {
        var (provider, handler) = CreateProvider(_ => Json(new TestProduct { Name = "New" }));

        var created = await provider.CreateAsync(new TestProduct { Name = "New" });

        Assert.Equal("New", created.Name);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("/api/base/products", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("New", handler.LastRequestBody);
    }

    [Fact]
    public async Task PatchAsync_SendsPatchModelToEntityEndpoint()
    {
        var id = Guid.NewGuid();
        var (provider, handler) = CreateProvider(_ => Json(new TestProduct { Name = "Patched" }));

        await provider.PatchAsync(id, new Dictionary<string, object?> { ["Name"] = "Patched" }, concurrencyStamp: "stamp-1");

        Assert.Equal(HttpMethod.Patch, handler.LastRequest.Method);
        Assert.Equal($"/api/base/products/{id}", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("changedFields", handler.LastRequestBody);
        Assert.Contains("concurrencyStamp", handler.LastRequestBody);
    }

    [Fact]
    public async Task PatchAsync_Conflict_ThrowsConcurrencyConflict()
    {
        var (provider, _) = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.Conflict));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => provider.PatchAsync(Guid.NewGuid(), new Dictionary<string, object?> { ["Name"] = "x" }));
    }

    [Fact]
    public async Task DeleteAsync_SendsDeleteToEntityEndpoint()
    {
        var id = Guid.NewGuid();
        var (provider, handler) = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await provider.DeleteAsync(id);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
        Assert.Equal($"/api/base/products/{id}", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    private static (HttpBaseDataProvider<TestProduct> Provider, RecordingHttpMessageHandler Handler) CreateProvider(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new RecordingHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        return (new HttpBaseDataProvider<TestProduct>(httpClient), handler);
    }

    private static HttpResponseMessage Json(object? body, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) { Content = JsonContent.Create(body) };
    }
}
