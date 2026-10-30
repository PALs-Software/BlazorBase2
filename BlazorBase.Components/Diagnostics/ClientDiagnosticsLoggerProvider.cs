using Microsoft.Extensions.Logging;

namespace BlazorBase.Components.Diagnostics;

/// <summary>
/// Records log entries of the configured level or above (errors by default) in an <see cref="IClientDiagnostics"/>.
/// Only meaningful when the diagnostics are shared by the whole process, i.e. in WebAssembly.
/// </summary>
public sealed class ClientDiagnosticsLoggerProvider(IClientDiagnostics diagnostics, ClientDiagnosticsOptions options) : ILoggerProvider
{
    #region Injects

    private readonly IClientDiagnostics Diagnostics = diagnostics;
    private readonly ClientDiagnosticsOptions Options = options;

    #endregion

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new DiagnosticsLogger(categoryName, Diagnostics, Options);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class DiagnosticsLogger(string category, IClientDiagnostics diagnostics, ClientDiagnosticsOptions options) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= options.MinimumLogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var text = exception is null ? message : $"{message} ({exception.GetType().Name}: {exception.Message})";
            diagnostics.RecordError("Logger", $"{category}: {text}");
        }
    }
}
