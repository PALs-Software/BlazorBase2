using System.Security.Claims;
using BlazorBase.Components.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Components.Test.Services;

/// <summary>
/// <see cref="LanguageService"/> applies the culture to the current thread and, separately, stamps
/// <c>&lt;html lang&gt;</c> so accessibility tooling and any other code reading the attribute sees the
/// active language instead of whatever the static markup shipped with - a native browser control such
/// as the built-in required-field validation bubble follows the browser's own UI language instead, so
/// this is not expected to change that one.
/// </summary>
public class LanguageServiceTests : BunitContext
{
    private const string DocumentLanguageIdentifier = "document.documentElement.setAttribute";

    [Fact]
    public void ApplyStartupLanguage_SetsCurrentLanguage()
    {
        JSInterop.SetupVoid(DocumentLanguageIdentifier, _ => true).SetVoidResult();
        var service = CreateService(claim: null);

        service.ApplyStartupLanguage("de");

        Assert.Equal("de", service.CurrentLanguage);
    }

    [Fact]
    public async Task SetLanguageAsync_StampsTheDocumentLanguage_WithTheNewCulture()
    {
        var handler = JSInterop.SetupVoid(DocumentLanguageIdentifier, _ => true);
        handler.SetVoidResult();
        var service = CreateService(claim: null);
        service.ApplyStartupLanguage("en");

        await service.SetLanguageAsync("de");

        Assert.Contains(handler.Invocations, invocation => invocation.Arguments is ["lang", "de"]);
    }

    [Fact]
    public async Task InitializeAsync_StampsTheDocumentLanguage_WithTheResolvedCulture()
    {
        var handler = JSInterop.SetupVoid(DocumentLanguageIdentifier, _ => true);
        handler.SetVoidResult();
        var service = CreateService(claim: "de");
        service.ApplyStartupLanguage("de");

        await service.InitializeAsync();

        Assert.Contains(handler.Invocations, invocation => invocation.Arguments is ["lang", "de"]);
    }

    /// <summary>
    /// A host whose JS interop channel is not ready yet (or not present at all) must not take the rest
    /// of the language switch down over what is purely a cosmetic touch.
    /// </summary>
    [Fact]
    public async Task SetLanguageAsync_StillChangesTheCulture_WhenTheDocumentStampFails()
    {
        JSInterop.SetupVoid(DocumentLanguageIdentifier, _ => true).SetException(new InvalidOperationException("JS interop unavailable."));
        var service = CreateService(claim: null);
        service.ApplyStartupLanguage("en");

        await service.SetLanguageAsync("de");

        Assert.Equal("de", service.CurrentLanguage);
    }

    private LanguageService CreateService(string? claim)
    {
        var authorization = this.AddAuthorization();

        if (claim is null)
            authorization.SetNotAuthorized();
        else
        {
            authorization.SetAuthorized("user");
            authorization.SetClaims(new Claim("language", claim));
        }

        var provider = Services.GetRequiredService<AuthenticationStateProvider>();
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        return new LanguageService(provider, navigationManager, Services.GetRequiredService<Microsoft.JSInterop.IJSRuntime>());
    }
}
