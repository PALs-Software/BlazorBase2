using System.Runtime.CompilerServices;
using System.Text;

namespace BlazorBase.Components.Assistant;

/// <summary>One message of a <c>text/event-stream</c>.</summary>
/// <param name="Event">The <c>event</c> field; null when the message has none.</param>
/// <param name="Data">The <c>data</c> lines joined with a line feed.</param>
public sealed record ServerSentEvent(string? Event, string Data);

/// <summary>Reads a <c>text/event-stream</c> message by message.</summary>
/// <remarks>
/// Follows the Server-Sent Events format: lines end with LF, CR or CRLF, a blank line ends a message, lines starting
/// with a colon are comments, several <c>data</c> lines make one multi-line value, and a message the stream ends
/// in the middle of is dropped.
/// </remarks>
public static class ServerSentEventReader
{
    /// <summary>Reads the messages of <paramref name="stream"/> until it ends or <paramref name="cancellationToken"/> is cancelled.</summary>
    public static async IAsyncEnumerable<ServerSentEvent> ReadAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var data = new StringBuilder();
        string? eventName = null;
        var hasData = false;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                if (hasData)
                    yield return new ServerSentEvent(eventName, data.ToString());

                data.Clear();
                eventName = null;
                hasData = false;
                continue;
            }

            if (line[0] == ':')
                continue;

            var separator = line.IndexOf(':');
            var field = separator < 0 ? line : line[..separator];
            var value = separator < 0 ? string.Empty : line[(separator + 1)..];

            if (value.StartsWith(' '))
                value = value[1..];

            if (field == "data")
            {
                if (hasData)
                    data.Append('\n');

                data.Append(value);
                hasData = true;
            }
            else if (field == "event")
            {
                eventName = value.Length == 0 ? null : value;
            }
        }
    }
}
