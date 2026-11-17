using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.Pages;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Pages;

/// <summary>
/// The OAuth consent page shows the self-asserted client name together with the redirect host the user
/// can actually check, leaves the application for the redirect URI the server returns after a decision,
/// and offers no buttons for an expired request or an account that may not authorize apps.
/// </summary>
public class OAuthConsentTests : UserBunitTestContextBase
{
    private const string ProtectedRequest = "sealed-request";

    private IOAuthConsentService RegisterConsentService()
    {
        Services.AddLocalization();
        var consentService = Substitute.For<IOAuthConsentService>();
        Services.AddSingleton(consentService);
        return consentService;
    }

    private IRenderedComponent<OAuthConsent> RenderConsentPage()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/oauth/consent?request={ProtectedRequest}");
        return Render<OAuthConsent>();
    }

    [Fact]
    public void Shows_the_client_name_the_redirect_host_and_the_scopes()
    {
        var consentService = RegisterConsentService();
        consentService.GetAuthorizationRequestAsync(ProtectedRequest).Returns(new OAuthAuthorizationRequestDetails
        {
            ClientName = "Claude",
            RedirectHost = "claude.ai",
            Scopes = [new OAuthScopeDetails { Name = "mcp", Description = "Server description" }],
        });

        var cut = RenderConsentPage();

        Assert.Contains("Claude", cut.Find(".oauth-consent-headline").TextContent);
        Assert.Equal("claude.ai", cut.Find(".oauth-consent-redirect-host").TextContent);
        Assert.Contains("mcp", cut.Find(".oauth-consent-scopes").TextContent);
        Assert.Equal(2, cut.FindComponents<FluentButton>().Count);
        Assert.Empty(cut.FindAll(".oauth-consent-client-id-host"));
    }

    [Fact]
    public void Shows_the_host_that_published_the_client_metadata_document()
    {
        var consentService = RegisterConsentService();
        consentService.GetAuthorizationRequestAsync(ProtectedRequest).Returns(new OAuthAuthorizationRequestDetails
        {
            ClientName = "Claude",
            RedirectHost = "claude.ai",
            ClientIdHost = "claude.ai",
        });

        var cut = RenderConsentPage();

        Assert.Equal("claude.ai", cut.Find(".oauth-consent-client-id-host").TextContent);
    }

    [Fact]
    public void Approving_leaves_for_the_returned_redirect_uri()
    {
        var consentService = RegisterConsentService();
        consentService.GetAuthorizationRequestAsync(ProtectedRequest).Returns(new OAuthAuthorizationRequestDetails { ClientName = "Claude", RedirectHost = "claude.ai" });
        consentService.SubmitDecisionAsync(Arg.Is<OAuthAuthorizationDecisionRequest>(decision => decision.Approved && decision.Request == ProtectedRequest))
            .Returns(new OAuthAuthorizationDecisionResponse { RedirectUri = "https://claude.ai/api/mcp/auth_callback?code=abc&state=xyz" });

        var cut = RenderConsentPage();
        cut.Find(".oauth-consent-approve").Click();

        Assert.Equal("https://claude.ai/api/mcp/auth_callback?code=abc&state=xyz", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void An_expired_request_shows_a_message_and_no_buttons()
    {
        var consentService = RegisterConsentService();
        consentService.GetAuthorizationRequestAsync(ProtectedRequest).Returns((OAuthAuthorizationRequestDetails?)null);

        var cut = RenderConsentPage();

        Assert.Single(cut.FindAll(".oauth-consent-status"));
        Assert.Empty(cut.FindComponents<FluentButton>());
    }

    [Fact]
    public void A_forbidden_account_sees_no_buttons()
    {
        var consentService = RegisterConsentService();
        consentService.GetAuthorizationRequestAsync(ProtectedRequest)
            .Returns<Task<OAuthAuthorizationRequestDetails?>>(_ => throw new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden));

        var cut = RenderConsentPage();

        Assert.Single(cut.FindAll(".oauth-consent-status"));
        Assert.Empty(cut.FindComponents<FluentButton>());
    }
}
