using System.Net;
using BlazorBase.Components.Routing;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Pages;

/// <summary>
/// The consent page of the OAuth authorization server: <c>api/oauth/authorize</c> sends the browser here
/// with a sealed <c>request</c>, the signed-in user sees which app asks for which scopes and where the
/// answer will be sent, and approves or denies. Either way the page then leaves the application for
/// the client's redirect URI. An unauthenticated visitor is sent through the login page and back.
/// </summary>
/// <remarks>
/// The client name is chosen by whoever registered the client, so the redirect host is shown next to
/// it: it is the one fact about the client the user can check. An expired or tampered request shows a
/// message and no buttons.
/// </remarks>
public partial class OAuthConsent(IOAuthConsentService consentService, NavigationManager navigation, IStringLocalizer<OAuthConsent> localizer)
{
    #region Injects
    private readonly IOAuthConsentService ConsentService = consentService;
    private readonly NavigationManager Navigation = navigation;
    private readonly IStringLocalizer<OAuthConsent> Localizer = localizer;
    #endregion

    private const string RequestParameterName = "request";

    private string? ProtectedRequest { get; set; }
    private OAuthAuthorizationRequestDetails? Details { get; set; }
    private string? StatusMessage { get; set; }
    private string? ErrorMessage { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsSubmitting { get; set; }

    protected override async Task OnInitializedAsync()
    {
        ProtectedRequest = QueryStringReader.TryReadValue(Navigation.Uri, RequestParameterName);

        if (string.IsNullOrEmpty(ProtectedRequest))
        {
            StatusMessage = Localizer["InvalidRequest"];
            IsLoading = false;
            return;
        }

        try
        {
            Details = await ConsentService.GetAuthorizationRequestAsync(ProtectedRequest);

            if (Details is null)
                StatusMessage = Localizer["InvalidRequest"];
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            StatusMessage = Localizer["NotPermitted"];
        }
        catch (HttpRequestException)
        {
            StatusMessage = Localizer["LoadError"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Task ApproveAsync() => SubmitDecisionAsync(approved: true);

    private Task DenyAsync() => SubmitDecisionAsync(approved: false);

    private async Task SubmitDecisionAsync(bool approved)
    {
        if (string.IsNullOrEmpty(ProtectedRequest))
            return;

        ErrorMessage = null;
        IsSubmitting = true;

        try
        {
            var response = await ConsentService.SubmitDecisionAsync(new OAuthAuthorizationDecisionRequest { Request = ProtectedRequest, Approved = approved });

            if (response is null)
            {
                ShowStatus(Localizer["InvalidRequest"]);
                return;
            }

            Navigation.NavigateTo(response.RedirectUri, forceLoad: true);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            ShowStatus(Localizer["NotPermitted"]);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = Localizer["SubmitError"];
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    /// <summary>Replaces the request details with <paramref name="message"/>, which also removes the buttons.</summary>
    private void ShowStatus(string message)
    {
        Details = null;
        StatusMessage = message;
    }

    /// <summary>
    /// Prefers a localized description (resource key <c>Scope_{name}</c>) over the server-configured one,
    /// so the default <c>mcp</c> scope reads naturally in every supported language.
    /// </summary>
    private string DescribeScope(OAuthScopeDetails scope)
    {
        var localizedDescription = Localizer[$"Scope_{scope.Name}"];
        return localizedDescription.ResourceNotFound ? scope.Description : localizedDescription.Value;
    }
}
