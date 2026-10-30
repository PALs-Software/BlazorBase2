using BlazorBase.Notifications.Interop;
using BlazorBase.Notifications.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace BlazorBase.Notifications.Components.PushSettingsToggle;

/// <summary>
/// A settings switch for native (Web Push) notifications on this device. Shows one of four states -
/// unsupported, denied, enabled, disabled - with explanatory text, and performs the full
/// subscribe/unsubscribe round trip (browser subscription plus the host's subscription endpoint)
/// when toggled. Requesting notification permission only ever happens inside the click handler that
/// turns the switch on, so the browser sees it as triggered by a user gesture.
/// </summary>
public partial class PushSettingsToggle(
    IWebPushClient webPushClient,
    IPushSubscriptionClient subscriptionClient,
    IStringLocalizer<PushSettingsToggle> localizer) : ComponentBase
{
    #region Injects
    private readonly IWebPushClient WebPushClient = webPushClient;
    private readonly IPushSubscriptionClient SubscriptionClient = subscriptionClient;
    private readonly IStringLocalizer<PushSettingsToggle> Localizer = localizer;
    #endregion

    /// <summary>Additional CSS classes for the wrapper.</summary>
    [Parameter]
    public string? Class { get; set; }

    private PushToggleState State { get; set; } = PushToggleState.Unsupported;

    private bool IsBusy { get; set; }

    private bool IsOn => State == PushToggleState.Enabled;

    private bool IsInteractive => !IsBusy && State is PushToggleState.Enabled or PushToggleState.Disabled;

    private string Caption => Localizer[$"State{State}"];

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        if (!await WebPushClient.IsSupportedAsync())
        {
            State = PushToggleState.Unsupported;
            return;
        }

        if (await WebPushClient.GetPermissionAsync() == Models.WebPushPermission.Denied)
        {
            State = PushToggleState.Denied;
            return;
        }

        var endpoint = await WebPushClient.GetCurrentEndpointAsync();
        State = endpoint is not null ? PushToggleState.Enabled : PushToggleState.Disabled;
    }

    private async Task ToggleAsync(bool turnOn)
    {
        if (IsBusy)
            return;

        IsBusy = true;

        if (turnOn)
            await EnableAsync();
        else
            await DisableAsync();

        IsBusy = false;
    }

    private async Task EnableAsync()
    {
        try
        {
            var publicKey = await SubscriptionClient.GetPublicKeyAsync();
            var subscription = await WebPushClient.SubscribeAsync(publicKey);
            await SubscriptionClient.SubscribeAsync(subscription);
            State = PushToggleState.Enabled;
        }
        catch (JSException)
        {
            State = await WebPushClient.GetPermissionAsync() == Models.WebPushPermission.Denied
                ? PushToggleState.Denied
                : PushToggleState.Disabled;
        }
    }

    private async Task DisableAsync()
    {
        var endpoint = await WebPushClient.GetCurrentEndpointAsync();
        await WebPushClient.UnsubscribeAsync();

        if (endpoint is not null)
            await SubscriptionClient.UnsubscribeAsync(endpoint);

        State = PushToggleState.Disabled;
    }
}
