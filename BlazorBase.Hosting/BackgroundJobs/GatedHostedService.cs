using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Hosting.BackgroundJobs;

internal sealed class GatedHostedService(string name, Func<IHostedService> innerFactory, IBackgroundJobGate gate, ILogger<GatedHostedService> logger) : IHostedService, IDisposable
{
    #region Injects
    private readonly string Name = name;
    private readonly Func<IHostedService> InnerFactory = innerFactory;
    private readonly IBackgroundJobGate Gate = gate;
    private readonly ILogger<GatedHostedService> Logger = logger;
    #endregion

    private IHostedService? Inner;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!Gate.IsEnabled)
        {
            Logger.LogInformation("Background job {Job} is not started because BackgroundJobs:Enabled is false.", Name);
            return Task.CompletedTask;
        }

        Inner = InnerFactory();
        return Inner.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Inner?.StopAsync(cancellationToken) ?? Task.CompletedTask;

    public void Dispose() => (Inner as IDisposable)?.Dispose();
}
