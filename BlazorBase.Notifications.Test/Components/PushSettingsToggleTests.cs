using BlazorBase.Components.Services;
using BlazorBase.Notifications.Components.PushSettingsToggle;
using BlazorBase.Notifications.Interop;
using BlazorBase.Notifications.Models;
using BlazorBase.Notifications.Services;
using BlazorBase.Notifications.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace BlazorBase.Notifications.Test.Components;

public sealed class PushSettingsToggleTests : BunitContext
{
    private readonly IWebPushClient WebPushClient = Substitute.For<IWebPushClient>();
    private readonly IPushSubscriptionClient SubscriptionClient = Substitute.For<IPushSubscriptionClient>();
    private readonly CultureScope Culture = new("en");

    public PushSettingsToggleTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazorBaseFluentUI();
        Services.AddLocalization();
        Services.AddSingleton(WebPushClient);
        Services.AddSingleton(SubscriptionClient);

        WebPushClient.IsSupportedAsync().Returns(true);
        WebPushClient.GetPermissionAsync().Returns(WebPushPermission.Default);
        WebPushClient.GetCurrentEndpointAsync().Returns((string?)null);
    }

    protected override void Dispose(bool disposing)
    {
        Culture.Dispose();
        base.Dispose(disposing);
    }

    private static FluentSwitch SwitchOf(IRenderedComponent<PushSettingsToggle> cut) => cut.FindComponent<FluentSwitch>().Instance;

    private static async Task ToggleAsync(IRenderedComponent<PushSettingsToggle> cut, bool turnOn)
    {
        var fluentSwitch = cut.FindComponent<FluentSwitch>();
        await cut.InvokeAsync(() => fluentSwitch.Instance.ValueChanged.InvokeAsync(turnOn));
    }

    [Fact]
    public void Unsupported_ShowsTheUnsupportedMessageAndDisablesTheSwitch()
    {
        WebPushClient.IsSupportedAsync().Returns(false);

        var cut = Render<PushSettingsToggle>();

        Assert.Equal("This browser does not support native notifications.", cut.Find(".push-settings-toggle-caption").TextContent);
        Assert.True(SwitchOf(cut).Disabled);
    }

    [Fact]
    public void Denied_ShowsTheDeniedMessageAndDisablesTheSwitch()
    {
        WebPushClient.GetPermissionAsync().Returns(WebPushPermission.Denied);

        var cut = Render<PushSettingsToggle>();

        Assert.Contains("blocked", cut.Find(".push-settings-toggle-caption").TextContent);
        Assert.True(SwitchOf(cut).Disabled);
    }

    [Fact]
    public void NoCurrentSubscription_ShowsTheDisabledMessageAndAnUncheckedSwitch()
    {
        var cut = Render<PushSettingsToggle>();

        Assert.Contains("Turn this on", cut.Find(".push-settings-toggle-caption").TextContent);
        Assert.False(SwitchOf(cut).Value);
        Assert.False(SwitchOf(cut).Disabled);
    }

    [Fact]
    public void ExistingSubscription_ShowsTheEnabledMessageAndACheckedSwitch()
    {
        WebPushClient.GetCurrentEndpointAsync().Returns("https://push.test/abc");

        var cut = Render<PushSettingsToggle>();

        Assert.Contains("You will receive", cut.Find(".push-settings-toggle-caption").TextContent);
        Assert.True(SwitchOf(cut).Value);
    }

    [Fact]
    public async Task TurningOn_SubscribesThroughTheInteropAndTheServer()
    {
        SubscriptionClient.GetPublicKeyAsync().Returns("vapid-key");
        var subscription = new PushSubscriptionDto("https://push.test/abc", "p", "a", "agent");
        WebPushClient.SubscribeAsync("vapid-key").Returns(subscription);
        var cut = Render<PushSettingsToggle>();

        await ToggleAsync(cut, true);

        await WebPushClient.Received(1).SubscribeAsync("vapid-key");
        await SubscriptionClient.Received(1).SubscribeAsync(subscription, Arg.Any<CancellationToken>());
        Assert.Contains("You will receive", cut.Find(".push-settings-toggle-caption").TextContent);
        Assert.True(SwitchOf(cut).Value);
    }

    [Fact]
    public async Task TurningOn_Denied_ShowsTheDeniedMessage()
    {
        WebPushClient.SubscribeAsync(Arg.Any<string>()).Returns<PushSubscriptionDto>(_ => throw new JSException("denied"));
        WebPushClient.GetPermissionAsync().Returns(WebPushPermission.Default, WebPushPermission.Denied);
        var cut = Render<PushSettingsToggle>();

        await ToggleAsync(cut, true);

        Assert.Contains("blocked", cut.Find(".push-settings-toggle-caption").TextContent);
        Assert.True(SwitchOf(cut).Disabled);
    }

    [Fact]
    public async Task TurningOff_UnsubscribesThroughTheInteropAndTheServer()
    {
        WebPushClient.GetCurrentEndpointAsync().Returns("https://push.test/abc");
        var cut = Render<PushSettingsToggle>();

        await ToggleAsync(cut, false);

        await WebPushClient.Received(1).UnsubscribeAsync();
        await SubscriptionClient.Received(1).UnsubscribeAsync("https://push.test/abc", Arg.Any<CancellationToken>());
        Assert.Contains("Turn this on", cut.Find(".push-settings-toggle-caption").TextContent);
        Assert.False(SwitchOf(cut).Value);
    }

    [Fact]
    public void Label_Override_ReplacesTheResourceLabel()
    {
        var cut = Render<PushSettingsToggle>(parameters => parameters.Add(component => component.Label, "Custom label"));

        Assert.Equal("Custom label", SwitchOf(cut).Label);
    }

    [Fact]
    public void Label_Default_UsesTheResourceLabel()
    {
        var cut = Render<PushSettingsToggle>();

        Assert.Equal("Native notifications on this device", SwitchOf(cut).Label);
    }

    [Fact]
    public void UnsupportedText_Override_ReplacesTheResourceCaption()
    {
        WebPushClient.IsSupportedAsync().Returns(false);

        var cut = Render<PushSettingsToggle>(parameters => parameters.Add(component => component.UnsupportedText, "Custom unsupported"));

        Assert.Equal("Custom unsupported", cut.Find(".push-settings-toggle-caption").TextContent);
    }

    [Fact]
    public void DeniedText_Override_ReplacesTheResourceCaption()
    {
        WebPushClient.GetPermissionAsync().Returns(WebPushPermission.Denied);

        var cut = Render<PushSettingsToggle>(parameters => parameters.Add(component => component.DeniedText, "Custom denied"));

        Assert.Equal("Custom denied", cut.Find(".push-settings-toggle-caption").TextContent);
    }

    [Fact]
    public void EnabledText_Override_ReplacesTheResourceCaption()
    {
        WebPushClient.GetCurrentEndpointAsync().Returns("https://push.test/abc");

        var cut = Render<PushSettingsToggle>(parameters => parameters.Add(component => component.EnabledText, "Custom enabled"));

        Assert.Equal("Custom enabled", cut.Find(".push-settings-toggle-caption").TextContent);
    }

    [Fact]
    public void DisabledText_Override_ReplacesTheResourceCaption()
    {
        var cut = Render<PushSettingsToggle>(parameters => parameters.Add(component => component.DisabledText, "Custom disabled"));

        Assert.Equal("Custom disabled", cut.Find(".push-settings-toggle-caption").TextContent);
    }

    [Fact]
    public void TextOverrides_OnlyAffectTheirOwnState()
    {
        var cut = Render<PushSettingsToggle>(parameters => parameters
            .Add(component => component.EnabledText, "Custom enabled")
            .Add(component => component.DeniedText, "Custom denied"));

        Assert.Contains("Turn this on", cut.Find(".push-settings-toggle-caption").TextContent);
    }

    [Fact]
    public void GermanCulture_UsesGermanTexts()
    {
        using var culture = new CultureScope("de");

        var cut = Render<PushSettingsToggle>();

        Assert.Contains("Aktivieren Sie dies", cut.Find(".push-settings-toggle-caption").TextContent);
    }
}
