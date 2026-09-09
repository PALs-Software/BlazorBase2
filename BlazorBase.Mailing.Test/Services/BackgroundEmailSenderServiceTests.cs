using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using BlazorBase.Mailing.Test.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Mailing.Test.Services;

/// <summary>
/// Tests for <see cref="BackgroundEmailSenderService"/>: it drains the queue through a fresh scope
/// per message, and a failing send does not stop it from processing the next message. Completion
/// is observed through a <see cref="TaskCompletionSource{TResult}"/> the stub sender fulfils,
/// awaited with a timeout guard, never by sleeping for a fixed duration.
/// </summary>
public sealed class BackgroundEmailSenderServiceTests
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);

    private static (BackgroundEmailSenderService Service, ChannelBackgroundEmailQueue Queue, EmailSendRecorder Recorder) CreateService(
        Func<int, Exception?>? exceptionForCall = null)
    {
        var recorder = new EmailSendRecorder();
        if (exceptionForCall is not null)
            recorder.ExceptionForCall = exceptionForCall;

        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddScoped<IEmailSender, RecordingEmailSender>();

        var provider = services.BuildServiceProvider();
        var queue = new ChannelBackgroundEmailQueue(Options.Create(new BackgroundEmailDispatchOptions()));

        var service = new BackgroundEmailSenderService(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BackgroundEmailSenderService>.Instance);

        return (service, queue, recorder);
    }

    private static async Task<EmailMessage> AwaitSend(Task<EmailMessage> sendTask)
    {
        var completed = await Task.WhenAny(sendTask, Task.Delay(SendTimeout));
        Assert.Same(sendTask, completed);
        return await sendTask;
    }

    [Fact]
    public async Task ExecuteAsync_SendsQueuedMessage_ThroughResolvedSender()
    {
        var (service, queue, recorder) = CreateService();
        var message = new EmailMessage { To = ["to@example.com"], Subject = "Hi" };
        var sendTask = recorder.WaitForSendAsync(message);

        await queue.EnqueueAsync(message);

        using var cancellation = new CancellationTokenSource();
        await service.StartAsync(cancellation.Token);
        try
        {
            var sent = await AwaitSend(sendTask);
            Assert.Same(message, sent);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesFreshScope_PerMessage()
    {
        var (service, queue, recorder) = CreateService();
        var first = new EmailMessage { To = ["first@example.com"], Subject = "First" };
        var second = new EmailMessage { To = ["second@example.com"], Subject = "Second" };
        var firstSendTask = recorder.WaitForSendAsync(first);
        var secondSendTask = recorder.WaitForSendAsync(second);

        await queue.EnqueueAsync(first);
        await queue.EnqueueAsync(second);

        using var cancellation = new CancellationTokenSource();
        await service.StartAsync(cancellation.Token);
        try
        {
            await AwaitSend(firstSendTask);
            await AwaitSend(secondSendTask);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        Assert.Equal(2, recorder.ResolvedSenders.Count);
        Assert.NotSame(recorder.ResolvedSenders[0], recorder.ResolvedSenders[1]);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesAfterFailedSend()
    {
        var (service, queue, recorder) = CreateService(callIndex => callIndex == 0
            ? new InvalidOperationException("transport unavailable")
            : null);

        var failing = new EmailMessage { To = ["fails@example.com"], Subject = "Fails" };
        var succeeding = new EmailMessage { To = ["ok@example.com"], Subject = "Ok" };
        var succeedingSendTask = recorder.WaitForSendAsync(succeeding);

        await queue.EnqueueAsync(failing);
        await queue.EnqueueAsync(succeeding);

        using var cancellation = new CancellationTokenSource();
        await service.StartAsync(cancellation.Token);
        try
        {
            var sent = await AwaitSend(succeedingSendTask);
            Assert.Same(succeeding, sent);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        Assert.Equal(2, recorder.SentMessages.Count);
    }
}
