using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.DataProviders;

/// <summary>
/// Data provider backed by HTTP REST API calls. Used in WASM/MAUI clients.
/// Relies on HttpClient configured with AuthTokenHandler for authentication.
/// </summary>
public class HttpBaseDataProvider<TModel>(
    HttpClient httpClient
) : IBaseDataProvider<TModel>
    where TModel : class
{
    #region Injects

    private readonly HttpClient HttpClient = httpClient;

    #endregion

    private string BaseUrl => (HttpClient.BaseAddress?.ToString() ?? string.Empty).TrimEnd('/');

    /// <remarks>
    /// <paramref name="scopeFilter"/> is ignored — row-level scoping is a server-side concern
    /// enforced by the endpoint; clients never construct or pass one.
    /// </remarks>
    public async Task<BaseQueryResult<TModel>> GetListAsync(BaseQuery query, Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"{BaseUrl}/query", query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BaseQueryResult<TModel>>(cancellationToken) ?? new BaseQueryResult<TModel>();
    }

    /// <remarks>
    /// <paramref name="scopeFilter"/> is ignored — row-level scoping is a server-side concern
    /// enforced by the endpoint; clients never construct or pass one.
    /// </remarks>
    public async Task<TModel?> GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/{id}";

        if (select is not null)
        {
            var fields = string.Join(',', select);
            url = $"{url}?select={Uri.EscapeDataString(fields)}";
        }

        var response = await HttpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TModel>(cancellationToken);
    }

    /// <remarks>
    /// <paramref name="scopeFilter"/> is ignored — row-level scoping is a server-side concern
    /// enforced by the endpoint; clients never construct or pass one.
    /// </remarks>
    public async Task<int> GetCountAsync(Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync($"{BaseUrl}/count", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(cancellationToken);
    }

    public async Task<TModel> CreateAsync(TModel model, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync(BaseUrl, model, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new BaseValidationException(await ReadValidationMessageAsync(response, cancellationToken));

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TModel>(cancellationToken)
            ?? throw new InvalidOperationException("Create returned null.");
    }

    public async Task<TModel> PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken cancellationToken = default)
    {
        var patchModel = new PatchModel
        {
            ChangedFields = changedFields,
            ConcurrencyStamp = concurrencyStamp
        };

        var response = await HttpClient.PatchAsJsonAsync($"{BaseUrl}/{id}", patchModel, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new ConcurrencyConflictException("The entity was modified by another user. Please reload and try again.");

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new BaseValidationException(await ReadValidationMessageAsync(response, cancellationToken));

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TModel>(cancellationToken)
            ?? throw new InvalidOperationException("Patch returned null.");
    }

    public async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"{BaseUrl}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new BaseValidationException(await ReadValidationMessageAsync(response, cancellationToken));

        response.EnsureSuccessStatusCode();
    }

    private const string DefaultValidationMessage = "The request was rejected.";

    private static async Task<string> ReadValidationMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            foreach (var propertyName in ValidationMessageProperties)
            {
                if (root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String)
                    return element.GetString() ?? DefaultValidationMessage;
            }
        }
        catch (JsonException)
        {
        }

        return DefaultValidationMessage;
    }

    private static readonly string[] ValidationMessageProperties = ["message", "detail", "title"];
}
