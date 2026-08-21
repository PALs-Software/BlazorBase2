using BlazorBase.Components.Services;
using BlazorBase.CRUD.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Extensions;

/// <summary>
/// The confirmation seam lives in BlazorBase.Components but is registered from the CRUD extension,
/// because a pure hosting backend calls neither and must not be made to supply an IDialogService.
/// That split is easy to break silently, so it is asserted rather than assumed.
/// </summary>
public class ConfirmationServiceRegistrationTests
{
    [Fact]
    public void AddBlazorBaseCrudComponents_ResolvesTheFluentUiImplementation()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Substitute.For<IDialogService>());

        services.AddBlazorBaseCrudComponents();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<FluentUiConfirmationService>(scope.ServiceProvider.GetRequiredService<IConfirmationService>());
    }

    /// <summary>
    /// The registration is a TryAdd so a host can put its own implementation in front of it.
    /// </summary>
    [Fact]
    public void AHostImplementation_RegisteredFirst_Wins()
    {
        var services = new ServiceCollection();
        var custom = Substitute.For<IConfirmationService>();
        services.AddScoped(_ => custom);

        services.AddBlazorBaseCrudComponents();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Same(custom, scope.ServiceProvider.GetRequiredService<IConfirmationService>());
    }
}
