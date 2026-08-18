using System.Security.Claims;
using BlazorBase.Components.Services;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Components.Test.Services;

/// <summary>
/// The preference has to survive both a reload and a sign-in. It lived only in memory before, so every
/// refresh silently reverted the user's choice.
/// </summary>
public class ThemeServiceTests : BunitContext
{
    private const string ModulePath = "./_content/BlazorBase.Components/js/theme.js";

    [Fact]
    public async Task Apply_WritesTheStoredPreferenceToTheDocument_ForAnAnonymousVisitor()
    {
        var module = SetupModule(stored: "Dark");
        var applyHandler = module.SetupVoid("apply", _ => true);
        applyHandler.SetVoidResult();
        var service = CreateService(claim: null);

        await service.InitializeAsync();
        await service.ApplyAsync();

        Assert.Equal(ThemePreference.Dark, service.CurrentTheme);
        Assert.Equal("Dark", Assert.Single(applyHandler.Invocations).Arguments[0]);
    }

    [Fact]
    public async Task Apply_KeepsTheProfilePreference_WhenTheBrowserRemembersSomethingElse()
    {
        var module = SetupModule(stored: "Light");
        var applyHandler = module.SetupVoid("apply", _ => true);
        applyHandler.SetVoidResult();
        var service = CreateService(claim: "Dark");

        await service.InitializeAsync();
        await service.ApplyAsync();

        Assert.Equal(ThemePreference.Dark, service.CurrentTheme);
        Assert.Equal("Dark", Assert.Single(applyHandler.Invocations).Arguments[0]);
    }

    [Fact]
    public async Task SetTheme_AppliesTheChoiceAndRaisesTheChange()
    {
        var module = SetupModule(stored: null);
        var applyHandler = module.SetupVoid("apply", _ => true);
        applyHandler.SetVoidResult();
        var service = CreateService(claim: null);
        var raised = 0;
        service.ThemeChanged += () => raised++;

        await service.SetThemeAsync(ThemePreference.Light);

        Assert.Equal(ThemePreference.Light, service.CurrentTheme);
        Assert.Equal(1, raised);
        Assert.Equal("Light", Assert.Single(applyHandler.Invocations).Arguments[0]);
    }

    [Fact]
    public async Task Apply_FallsBackToSystem_WhenNothingIsStored()
    {
        var module = SetupModule(stored: null);
        var applyHandler = module.SetupVoid("apply", _ => true);
        applyHandler.SetVoidResult();
        var service = CreateService(claim: null);

        await service.InitializeAsync();
        await service.ApplyAsync();

        Assert.Equal(ThemePreference.System, service.CurrentTheme);
        Assert.Equal("System", Assert.Single(applyHandler.Invocations).Arguments[0]);
    }

    private BunitJSModuleInterop SetupModule(string? stored)
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.Setup<string?>("stored").SetResult(stored);
        return module;
    }

    private ThemeService CreateService(string? claim)
    {
        var authorization = this.AddAuthorization();

        if (claim is null)
            authorization.SetNotAuthorized();
        else
        {
            authorization.SetAuthorized("user");
            authorization.SetClaims(new Claim("themePreference", claim));
        }

        var provider = Services.GetRequiredService<AuthenticationStateProvider>();
        return new ThemeService(provider, Services.GetRequiredService<IJSRuntime>());
    }
}
