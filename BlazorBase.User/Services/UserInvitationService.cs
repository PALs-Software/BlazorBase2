using System.Net.Http.Json;
using System.Text.Json;
using BlazorBase.User.Models;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Services;

/// <summary>
/// <see cref="IUserInvitationService"/> over the typed <see cref="HttpClient"/> the host registers, calling
/// <see cref="BlazorBaseUserClientOptions.InvitationEndpointBasePath"/>.
/// </summary>
public class UserInvitationService(HttpClient httpClient, IOptions<BlazorBaseUserClientOptions> clientOptions) : IUserInvitationService
{
    #region Injects
    private readonly HttpClient HttpClient = httpClient;
    private readonly string EndpointBasePath = clientOptions.Value.InvitationEndpointBasePath.Trim('/');
    #endregion

    public async Task<IReadOnlyList<UserInvitationSummary>> GetInvitationsAsync()
    {
        var response = await HttpClient.GetAsync(EndpointBasePath);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<UserInvitationSummary>>() ?? [];
    }

    public async Task<UserInvitationLinkResponse> CreateAsync(CreateUserInvitationRequest request) =>
        await ReadLinkResponseAsync(await HttpClient.PostAsJsonAsync(EndpointBasePath, request));

    public async Task<UserInvitationLinkResponse> ResendAsync(Guid invitationId) =>
        await ReadLinkResponseAsync(await HttpClient.PostAsync($"{EndpointBasePath}/{invitationId}/resend", content: null));

    public async Task<UserInvitationLinkResponse> RenewLinkAsync(Guid invitationId) =>
        await ReadLinkResponseAsync(await HttpClient.PostAsync($"{EndpointBasePath}/{invitationId}/link", content: null));

    public async Task RevokeAsync(Guid invitationId)
    {
        var response = await HttpClient.DeleteAsync($"{EndpointBasePath}/{invitationId}");
        await EnsureSuccessAsync(response);
    }

    public async Task<UserInvitationPreview> GetPreviewAsync(string token)
    {
        var response = await HttpClient.GetAsync($"{EndpointBasePath}/accept?token={Uri.EscapeDataString(token)}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<UserInvitationPreview>() ?? new UserInvitationPreview();
    }

    public async Task<LoginResponse> AcceptAsync(AcceptUserInvitationRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync($"{EndpointBasePath}/accept", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("Invalid accept response.");
    }

    private static async Task<UserInvitationLinkResponse> ReadLinkResponseAsync(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<UserInvitationLinkResponse>()
            ?? throw new InvalidOperationException("Invalid invitation response.");
    }

    /// <summary>
    /// Turns a problem details answer with a <c>code</c> into <see cref="UserInvitationException"/>; any other
    /// failure (for example <c>401</c>, <c>403</c> or <c>429</c>) stays a plain <see cref="HttpRequestException"/>.
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var exception = await TryReadProblemAsync(response);
        if (exception is not null)
            throw exception;

        response.EnsureSuccessStatusCode();
    }

    private static async Task<UserInvitationException?> TryReadProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("code", out var code))
                return null;

            return new UserInvitationException(code.GetString(), ReadDetail(root), ReadErrors(root), response.StatusCode);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadDetail(JsonElement root) =>
        root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String
            ? detail.GetString() ?? string.Empty
            : string.Empty;

    private static List<string> ReadErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            return [];

        return errors.EnumerateObject()
            .Where(field => field.Value.ValueKind == JsonValueKind.Array)
            .SelectMany(field => field.Value.EnumerateArray())
            .Select(message => message.GetString())
            .OfType<string>()
            .ToList();
    }
}
