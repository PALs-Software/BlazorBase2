using Microsoft.Extensions.Logging;

namespace BlazorBase.Mailing.Test.Infrastructure;

/// <summary>
/// A single log entry captured by <see cref="CapturingLogger{TCategoryName}"/>.
/// </summary>
public sealed record CapturedLogEntry(LogLevel Level, string Message);
