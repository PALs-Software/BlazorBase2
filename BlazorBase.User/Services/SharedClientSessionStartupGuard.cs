using Microsoft.Extensions.Hosting;

namespace BlazorBase.User.Services;

/// <summary>
/// Fails the start of a host that has the shared client session registered in an ASP.NET Core server
/// process. WebAssembly and MAUI never start hosted services, so the registration is inert there; in a
/// server the exception from <see cref="StartAsync"/> keeps the host from starting.
/// </summary>
internal sealed class SharedClientSessionStartupGuard(IServiceProvider serviceProvider) : IHostedService
{
    #region Injects
    private readonly IServiceProvider ServiceProvider = serviceProvider;
    #endregion

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SharedClientSessionGuard.ThrowIfServerProcess(ServiceProvider);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
