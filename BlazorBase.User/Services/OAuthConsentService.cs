using System.Net;
using System.Net.Http.Json;
using BlazorBase.User.Models;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Services;

/// <summary>
/// <see cref="IOAuthConsentService"/> over the typed <see cref="HttpClient"/> the host registers, calling
/// <c>{OAuthEndpointBasePath}/authorization-requests</c> and <c>{OAuthEndpointBasePath}/grants</c>.
/// </summary>
public class OAuthConsentService(HttpClient httpClient, IOptions<BlazorBaseUserClientOptions> clientOptions) : IOAuthConsentService
{
    #region Injects
    private readonly HttpClient HttpClient = httpClient;
    private readonly string EndpointBasePath = clientOptions.Value.OAuthEndpointBasePath.Trim('/');
    #endregion

    public async Task<OAuthAuthorizationRequestDetails?> GetAuthorizationRequestAsync(string request)
    {
        var response = await HttpClient.GetAsync($"{EndpointBasePath}/authorization-requests?request={Uri.EscapeDataString(request)}");

        if (response.StatusCode == HttpStatusCode.BadRequest)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OAuthAuthorizationRequestDetails>()
            ?? throw new InvalidOperationException("Invalid authorization request response.");
    }

    public async Task<OAuthAuthorizationDecisionResponse?> SubmitDecisionAsync(OAuthAuthorizationDecisionRequest decision)
    {
        var response = await HttpClient.PostAsJsonAsync($"{EndpointBasePath}/authorization-requests/decision", decision);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OAuthAuthorizationDecisionResponse>()
            ?? throw new InvalidOperationException("Invalid authorization decision response.");
    }

    public async Task<IReadOnlyList<OAuthGrantSummary>> GetGrantsAsync()
    {
        var response = await HttpClient.GetAsync($"{EndpointBasePath}/grants");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<OAuthGrantSummary>>() ?? [];
    }

    public async Task RevokeGrantAsync(Guid grantId)
    {
        var response = await HttpClient.DeleteAsync($"{EndpointBasePath}/grants/{grantId}");
        response.EnsureSuccessStatusCode();
    }
}
