using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Mailing.Test.Services;

/// <summary>
/// Tests for <see cref="ChannelBackgroundEmailQueue"/>: that a dequeued message is the same
/// instance that was enqueued, i.e. the queue neither copies nor mutates it.
/// </summary>
public sealed class ChannelBackgroundEmailQueueTests
{
    private static ChannelBackgroundEmailQueue CreateQueue(int capacity = 10) =>
        new(Options.Create(new BackgroundEmailDispatchOptions { QueueCapacity = capacity }));

    [Fact]
    public async Task DequeueAllAsync_ReturnsSameInstance_AsEnqueued()
    {
        var queue = CreateQueue();
        var message = new EmailMessage { To = ["to@example.com"], Subject = "Hi" };

        await queue.EnqueueAsync(message);

        using var cancellation = new CancellationTokenSource();
        var enumerator = queue.DequeueAllAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Same(message, enumerator.Current);
    }
}
