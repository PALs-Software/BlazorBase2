using BlazorBase.Components.Services;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Components;

/// <summary>
/// Lists the apps the signed-in user has connected through the OAuth authorization server and lets
/// them disconnect one after confirming. Disconnecting revokes the grant on the server, which stops
/// every token the app holds at once. Place it on a host settings page; it needs a registered
/// <see cref="IOAuthConsentService"/> and <see cref="IConfirmationService"/>.
/// </summary>
public partial class OAuthConnectedAppsPanel(
    IOAuthConsentService consentService,
    IConfirmationService confirmationService,
    IStringLocalizer<OAuthConnectedAppsPanel> localizer)
{
    #region Injects
    private readonly IOAuthConsentService ConsentService = consentService;
    private readonly IConfirmationService ConfirmationService = confirmationService;
    private readonly IStringLocalizer<OAuthConnectedAppsPanel> Localizer = localizer;
    #endregion

    private IReadOnlyList<OAuthGrantSummary> Grants { get; set; } = [];
    private string? ErrorMessage { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsRevoking { get; set; }

    protected override async Task OnInitializedAsync() => await LoadGrantsAsync();

    private async Task LoadGrantsAsync()
    {
        IsLoading = true;

        try
        {
            Grants = await ConsentService.GetGrantsAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = Localizer["LoadError"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RevokeAsync(OAuthGrantSummary grant)
    {
        var confirmed = await ConfirmationService.ConfirmDestructiveAsync(
            string.Format(Localizer["ConfirmMessage"], grant.ClientName),
            Localizer["ConfirmTitle"],
            Localizer["Revoke"],
            Localizer["Cancel"]);

        if (!confirmed)
            return;

        ErrorMessage = null;
        IsRevoking = true;

        try
        {
            await ConsentService.RevokeGrantAsync(grant.Id);
            await LoadGrantsAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = Localizer["RevokeError"];
        }
        finally
        {
            IsRevoking = false;
        }
    }

    private string FormatDate(DateTime? utcDate) =>
        utcDate is null
            ? Localizer["NeverUsed"]
            : DateTime.SpecifyKind(utcDate.Value, DateTimeKind.Utc).ToLocalTime().ToString("g");
}
