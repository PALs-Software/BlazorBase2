using BlazorBase.Notifications.Components.PushNavigationListener;
using BlazorBase.Notifications.Interop;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace BlazorBase.Notifications.Test.Components;

public sealed class PushNavigationListenerTests : BunitContext
{
    private readonly IPushNavigationInterop NavigationInterop = Substitute.For<IPushNavigationInterop>();

    public PushNavigationListenerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(NavigationInterop);
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private async Task<IRenderedComponent<PushNavigationListener>> RenderAndNavigateAsync(string url)
    {
        var cut = Render<PushNavigationListener>();
        await cut.InvokeAsync(() => cut.Instance.OnNavigateRequested(url));
        return cut;
    }

    [Fact]
    public void FirstRender_RegistersTheNavigationListener()
    {
        Render<PushNavigationListener>();

        NavigationInterop.Received(1).RegisterAsync(Arg.Any<DotNetObjectReference<PushNavigationListener>>());
    }

    [Fact]
    public async Task RelativePath_NavigatesInternally()
    {
        await RenderAndNavigateAsync("/dashboard?tab=2");

        var fakeNavigation = Assert.IsType<BunitNavigationManager>(Navigation);
        var entry = Assert.Single(fakeNavigation.History);
        Assert.Equal("dashboard?tab=2", entry.Uri);
        Assert.False(entry.Options.ForceLoad);
    }

    [Fact]
    public async Task AbsoluteSameOriginUrl_NavigatesToItsBaseRelativePath()
    {
        await RenderAndNavigateAsync("http://localhost/dashboard#section");

        Assert.Equal("http://localhost/dashboard#section", Navigation.Uri);
    }

    [Fact]
    public async Task AbsoluteUrlOnAnotherOrigin_IsIgnored()
    {
        var before = Navigation.Uri;

        await RenderAndNavigateAsync("https://elsewhere.example/dashboard");

        Assert.Equal(before, Navigation.Uri);
    }

    [Fact]
    public async Task Dispose_DisposesTheInterop()
    {
        var cut = Render<PushNavigationListener>();

        await DisposeComponentsAsync();

        await NavigationInterop.Received(1).DisposeAsync();
        Assert.NotNull(cut);
    }
}
