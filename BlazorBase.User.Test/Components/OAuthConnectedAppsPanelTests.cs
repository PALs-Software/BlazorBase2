using BlazorBase.Components.Services;
using BlazorBase.User.Components;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Components;

/// <summary>
/// The connected-apps panel lists the user's grants and disconnects one only after the user confirmed.
/// </summary>
public class OAuthConnectedAppsPanelTests : UserBunitTestContextBase
{
    private static readonly OAuthGrantSummary Grant = new()
    {
        Id = Guid.NewGuid(),
        ClientName = "Claude",
        RedirectHost = "claude.ai",
        Scope = "mcp",
        CreatedAt = DateTime.UtcNow,
    };

    private (IOAuthConsentService ConsentService, IConfirmationService ConfirmationService) RegisterServices(bool confirms)
    {
        Services.AddLocalization();

        var consentService = Substitute.For<IOAuthConsentService>();
        consentService.GetGrantsAsync().Returns(new List<OAuthGrantSummary> { Grant }, new List<OAuthGrantSummary>());
        Services.AddSingleton(consentService);

        var confirmationService = Substitute.For<IConfirmationService>();
        confirmationService.ConfirmDestructiveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(confirms);
        Services.AddSingleton(confirmationService);

        return (consentService, confirmationService);
    }

    [Fact]
    public void Lists_the_connected_apps()
    {
        RegisterServices(confirms: false);

        var cut = Render<OAuthConnectedAppsPanel>();

        Assert.Equal("Claude", cut.Find(".oauth-connected-app-name").TextContent);
        Assert.Contains("claude.ai", cut.Find(".oauth-connected-app-details").TextContent);
    }

    [Fact]
    public void Revokes_only_after_confirmation_and_reloads_the_list()
    {
        var (consentService, _) = RegisterServices(confirms: true);

        var cut = Render<OAuthConnectedAppsPanel>();
        cut.Find(".oauth-connected-app-revoke").Click();

        consentService.Received(1).RevokeGrantAsync(Grant.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".oauth-connected-apps-empty")));
    }

    [Fact]
    public void A_declined_confirmation_keeps_the_app()
    {
        var (consentService, _) = RegisterServices(confirms: false);

        var cut = Render<OAuthConnectedAppsPanel>();
        cut.Find(".oauth-connected-app-revoke").Click();

        consentService.DidNotReceive().RevokeGrantAsync(Arg.Any<Guid>());
        Assert.Single(cut.FindAll(".oauth-connected-app"));
    }
}
