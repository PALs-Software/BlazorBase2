using System.Net;
using System.Net.Http.Json;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Stores saved filters on the server through the endpoints of <c>MapBlazorBaseListFilters</c>. Used by WASM and MAUI
/// clients; the server derives the user from the authenticated request, so the client never names one.
/// </summary>
/// <remarks>
/// The <see cref="HttpClient"/> base address must point at the list-filter route and end with a slash.
/// </remarks>
public sealed class HttpListFilterStore(HttpClient httpClient) : IListFilterStore
{
    #region Injects

    private readonly HttpClient HttpClient = httpClient;

    #endregion

    public async Task<IReadOnlyList<SavedListFilterInfo>> GetAsync(string listKey, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync(BuildUrl(String.Empty, listKey), cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<SavedListFilterInfo>>(cancellationToken) ?? [];
    }

    public async Task<SavedListFilterInfo> SaveAsync(string listKey, SavedListFilterInput input, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync(BuildUrl(String.Empty, listKey), input, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new ListFilterNameConflictException(input.Name);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException("The saved filter no longer exists.");

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ListFilterValidationException(await ReadMessageAsync(response, cancellationToken));

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SavedListFilterInfo>(cancellationToken)
            ?? throw new InvalidOperationException("Save returned null.");
    }

    public async Task<bool> DeleteAsync(string listKey, Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync(BuildUrl(id.ToString("D"), listKey), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> SetDefaultAsync(string listKey, Guid? id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync(
            BuildUrl("default", listKey),
            new SetListFilterDefaultRequest { Id = id },
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    private static string BuildUrl(string path, string listKey) =>
        $"{path}?listKey={Uri.EscapeDataString(listKey)}";

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return String.IsNullOrWhiteSpace(text) ? "The filter was rejected." : text.Trim('"');
    }
}
