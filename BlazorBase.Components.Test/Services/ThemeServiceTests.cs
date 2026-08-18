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

    /// <summary>
    /// The profile seeds a browser that has no choice of its own; an explicit choice made in this
    /// browser wins afterwards, or clicking the toggle would revert on the next reload.
    /// </summary>
    [Fact]
    public async Task Apply_PrefersTheBrowsersChoice_OverTheProfilePreference()
    {
        var module = SetupModule(stored: "Light");
        var applyHandler = module.SetupVoid("apply", _ => true);
        applyHandler.SetVoidResult();
        var service = CreateService(claim: "Dark");

        await service.InitializeAsync();
        await service.ApplyAsync();

        Assert.Equal(ThemePreference.Light, service.CurrentTheme);
        Assert.Equal("Light", Assert.Single(applyHandler.Invocations).Arguments[0]);
    }

    [Fact]
    public async Task Apply_SeedsFromTheProfile_WhenTheBrowserHasNoChoiceYet()
    {
        var module = SetupModule(stored: null);
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

    /// <summary>
    /// Only an explicit choice is remembered. Persisting the profile's seed would make it outrank the
    /// profile from then on, so a change made on another device would never reach this browser again.
    /// </summary>
    [Fact]
    public async Task Apply_DoesNotPersistTheSeed_ButSetThemeDoes()
    {
        var module = SetupModule(stored: null);
        var applyHandler = module.SetupVoid("apply", _ => true);
        applyHandler.SetVoidResult();
        var service = CreateService(claim: "Dark");

        await service.InitializeAsync();
        await service.ApplyAsync();
        Assert.Equal(false, Assert.Single(applyHandler.Invocations).Arguments[1]);

        await service.SetThemeAsync(ThemePreference.Light);
        Assert.Equal(true, applyHandler.Invocations.Last().Arguments[1]);
    }

    private BunitJSModuleInterop SetupModule(string? stored)
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.Setup<string?>("stored").SetResult(stored);
        module.Setup<string?>("readToken", _ => true).SetResult("#0F766E");
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
