using Microsoft.Extensions.Options;

namespace BlazorBase.Hosting.BackgroundJobs;

/// <summary>Reads <see cref="BackgroundJobOptions.Enabled"/> on every call, so a configuration reload takes effect.</summary>
public sealed class ConfigurationBackgroundJobGate(IOptionsMonitor<BackgroundJobOptions> options) : IBackgroundJobGate
{
    #region Injects
    private readonly IOptionsMonitor<BackgroundJobOptions> Options = options;
    #endregion

    public bool IsEnabled => Options.CurrentValue.Enabled;
}
