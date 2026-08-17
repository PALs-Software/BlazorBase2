using Microsoft.Extensions.Logging;

namespace BlazorBase.Mailing.Test.Infrastructure;

/// <summary>
/// <see cref="ILogger{TCategoryName}"/> test double that records every log entry (level and
/// formatted message) so tests can assert on log content, e.g. that no PII leaks into a given
/// log level.
/// </summary>
public sealed class CapturingLogger<TCategoryName> : ILogger<TCategoryName>
{
    public List<CapturedLogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add(new CapturedLogEntry(logLevel, formatter(state, exception)));
}
