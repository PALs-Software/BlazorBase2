namespace BlazorBase.Components.Diagnostics;

/// <summary>One recorded client-side problem.</summary>
/// <param name="TimeUtc">When it was recorded.</param>
/// <param name="Source">Where it came from, e.g. <c>ErrorBoundary</c>, <c>Logger</c> or <c>Http</c>.</param>
/// <param name="Message">Exception type and message or the failed request, without a stack trace or query string.</param>
public sealed record ClientDiagnosticEntry(DateTimeOffset TimeUtc, string Source, string Message);

/// <summary>
/// A small in-memory record of the last client errors and the last correlation id, used to enrich issue reports
/// and assistant context. It never leaves the client on its own.
/// </summary>
public interface IClientDiagnostics
{
    /// <summary>The newest correlation id seen on a server response, or null.</summary>
    string? LastCorrelationId { get; }

    /// <summary>The recorded errors, oldest first; at most <see cref="ClientDiagnosticsOptions.Capacity"/>.</summary>
    IReadOnlyList<ClientDiagnosticEntry> Errors { get; }

    /// <summary>Raised after an error or a correlation id was recorded.</summary>
    event Action? Changed;

    /// <summary>Records an error message; the oldest entry is dropped when the buffer is full.</summary>
    void RecordError(string source, string message);

    /// <summary>Records an exception as <c>TypeName: message</c>.</summary>
    void RecordError(string source, Exception exception);

    /// <summary>Remembers the correlation id of the latest server response; blank values are ignored.</summary>
    void RecordCorrelationId(string? correlationId);

    /// <summary>Empties the buffer and forgets the correlation id.</summary>
    void Clear();
}
