using System.Threading.Channels;
using BlazorBase.Mailing.Models;
using Microsoft.Extensions.Options;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// <see cref="IBackgroundEmailQueue"/> backed by a bounded <see cref="Channel{T}"/>. Bounded with
/// <see cref="BoundedChannelFullMode.Wait"/> rather than unbounded or a drop mode: an unbounded
/// channel lets a stuck transport exhaust memory, and silently discarding a message (e.g. a
/// password-reset mail) is worse than making the producer wait. A full queue is a saturation
/// condition that affects every caller equally, not a signal about any one message or address, so
/// back-pressure is the correct response.
/// </summary>
public class ChannelBackgroundEmailQueue(IOptions<BackgroundEmailDispatchOptions> options) : IBackgroundEmailQueue
{
    private readonly Channel<EmailMessage> Channel = System.Threading.Channels.Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        Channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<EmailMessage> DequeueAllAsync(CancellationToken cancellationToken) =>
        Channel.Reader.ReadAllAsync(cancellationToken);
}
