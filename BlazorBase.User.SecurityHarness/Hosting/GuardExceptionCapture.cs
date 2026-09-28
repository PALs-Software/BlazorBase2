using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>
/// Captures the message of every logged exception from one profile's host, so
/// <see cref="HarnessHostFactory"/> can tell a startup guard that fires on the first request -
/// rather than during <c>host.StartAsync()</c> - apart from a host that is genuinely up and
/// serving requests. Registered as an <see cref="ILoggerProvider"/> on the profile's own
/// <see cref="Microsoft.Extensions.Hosting.IHost"/>; it observes what the framework already logs
/// and never touches the framework itself.
/// </summary>
internal sealed class GuardExceptionCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<string> Messages = new();

    public string? FirstCapturedMessage => Messages.TryPeek(out var message) ? message : null;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null)
                messages.Enqueue(exception.Message);
        }
    }
}
