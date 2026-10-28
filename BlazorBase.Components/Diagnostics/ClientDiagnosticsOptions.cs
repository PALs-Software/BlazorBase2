using Microsoft.Extensions.Logging;

namespace BlazorBase.Components.Diagnostics;

/// <summary>Settings of the <see cref="IClientDiagnostics"/> buffer and its recorders.</summary>
public sealed class ClientDiagnosticsOptions
{
    /// <summary>The most errors kept; the oldest is dropped first.</summary>
    public int Capacity { get; set; } = 10;

    /// <summary>Longer messages are cut to this length with an ellipsis.</summary>
    public int MaxMessageLength { get; set; } = 300;

    /// <summary>The lowest log level the logger provider records.</summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Error;

    /// <summary>The response headers read for the correlation id, first match wins.</summary>
    public IList<string> CorrelationHeaders { get; set; } = ["X-Correlation-ID", "X-Request-ID", "Request-Id"];
}
