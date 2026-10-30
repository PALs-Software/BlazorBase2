using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Diagnostics;

/// <summary>Thread-safe ring buffer implementation of <see cref="IClientDiagnostics"/>.</summary>
public sealed class ClientDiagnostics(IOptions<ClientDiagnosticsOptions> options) : IClientDiagnostics
{
    #region Injects

    private readonly ClientDiagnosticsOptions Options = options.Value;

    #endregion

    #region Members

    private readonly object Gate = new();
    private readonly Queue<ClientDiagnosticEntry> Entries = new();
    private string? CorrelationId;

    #endregion

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public string? LastCorrelationId
    {
        get
        {
            lock (Gate)
                return CorrelationId;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ClientDiagnosticEntry> Errors
    {
        get
        {
            lock (Gate)
                return Entries.ToArray();
        }
    }

    /// <inheritdoc />
    public void RecordError(string source, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var entry = new ClientDiagnosticEntry(DateTimeOffset.UtcNow, source, Shorten(message));
        lock (Gate)
        {
            Entries.Enqueue(entry);
            while (Entries.Count > Math.Max(1, Options.Capacity))
                Entries.Dequeue();
        }

        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void RecordError(string source, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        RecordError(source, $"{exception.GetType().Name}: {exception.Message}");
    }

    /// <inheritdoc />
    public void RecordCorrelationId(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return;

        lock (Gate)
            CorrelationId = correlationId.Trim();

        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (Gate)
        {
            Entries.Clear();
            CorrelationId = null;
        }

        Changed?.Invoke();
    }

    private string Shorten(string message)
    {
        var singleLine = string.Join(' ', message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var limit = Math.Max(10, Options.MaxMessageLength);

        return singleLine.Length <= limit ? singleLine : string.Concat(singleLine.AsSpan(0, limit - 1), "…");
    }
}
