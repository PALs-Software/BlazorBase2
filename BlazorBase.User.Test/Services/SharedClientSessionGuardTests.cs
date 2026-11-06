using BlazorBase.User.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// The shared client session makes the antiforgery token and the authentication state one instance for
/// the whole process. That is right for WebAssembly and MAUI and a cross-user leak in a process that
/// hosts an ASP.NET Core server, so there the registration has to fail loudly.
/// </summary>
public sealed class SharedClientSessionGuardTests
{
    private const string SharedSessionCallName = "AddBlazorBaseUserSharedClientSession";
    private const string StartupGuardTypeName = "SharedClientSessionStartupGuard";

    private static WebApplication BuildServerApplication(Action<IServiceCollection> configureServices)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        configureServices(builder.Services);
        return builder.Build();
    }

    private static int CountStartupGuards(IServiceCollection services) =>
        services.Count(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType?.Name == StartupGuardTypeName);

    [Fact]
    public async Task Server_process_the_host_does_not_start_with_the_shared_session_registered()
    {
        await using var application = BuildServerApplication(services => services.AddBlazorBaseUserSharedClientSession());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => application.StartAsync());

        Assert.Contains(SharedSessionCallName, exception.Message);
    }

    [Fact]
    public async Task Server_process_the_shared_session_state_cannot_be_constructed()
    {
        await using var application = BuildServerApplication(services => services.AddBlazorBaseUserSharedClientSession());

        var sessionStateException = Assert.Throws<InvalidOperationException>(() =>
            application.Services.GetRequiredService<AuthenticationSessionState>());
        var csrfTokenException = Assert.Throws<InvalidOperationException>(() =>
            application.Services.GetRequiredService<CsrfTokenProvider>());

        Assert.Contains(SharedSessionCallName, sessionStateException.Message);
        Assert.Contains(SharedSessionCallName, csrfTokenException.Message);
    }

    [Fact]
    public async Task Server_process_without_the_shared_session_keeps_the_state_scoped()
    {
        await using var application = BuildServerApplication(services => services.AddBlazorBaseUserClient());
        await using var firstScope = application.Services.CreateAsyncScope();
        await using var secondScope = application.Services.CreateAsyncScope();

        var firstSessionState = firstScope.ServiceProvider.GetRequiredService<AuthenticationSessionState>();
        var firstCsrfTokenProvider = firstScope.ServiceProvider.GetRequiredService<CsrfTokenProvider>();

        Assert.NotNull(firstSessionState);
        Assert.NotNull(firstCsrfTokenProvider);
        Assert.NotSame(firstSessionState, secondScope.ServiceProvider.GetRequiredService<AuthenticationSessionState>());
        Assert.NotSame(firstCsrfTokenProvider, secondScope.ServiceProvider.GetRequiredService<CsrfTokenProvider>());
    }

    [Fact]
    public async Task Client_process_shares_the_state_and_the_startup_guard_stays_inert()
    {
        var services = new ServiceCollection();
        services.AddBlazorBaseUserSharedClientSession();
        await using var provider = services.BuildServiceProvider();
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();

        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<AuthenticationSessionState>(),
            secondScope.ServiceProvider.GetRequiredService<AuthenticationSessionState>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<CsrfTokenProvider>(),
            secondScope.ServiceProvider.GetRequiredService<CsrfTokenProvider>());

        foreach (var hostedService in provider.GetServices<IHostedService>())
            await hostedService.StartAsync(CancellationToken.None);
    }

    [Fact]
    public void Calling_the_shared_session_registration_twice_registers_one_startup_guard()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseUserSharedClientSession();
        services.AddBlazorBaseUserSharedClientSession();

        Assert.Equal(1, CountStartupGuards(services));
    }
}
