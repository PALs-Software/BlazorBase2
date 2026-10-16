using BlazorBase.Notifications.Server.Entities;
using BlazorBase.Notifications.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BlazorBase.Notifications.Server.Test.Services;

public sealed class PushNotifierTests
{
    private readonly IPushSubscriptionStore Store = Substitute.For<IPushSubscriptionStore>();
    private readonly IWebPushSender Sender = Substitute.For<IWebPushSender>();

    private PushNotifier CreateNotifier() => new(Store, Sender, NullLogger<PushNotifier>.Instance);

    private static PushSubscription MakeSubscription(Guid userId, bool enabled = true) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Endpoint = $"https://push.example.test/{Guid.NewGuid()}",
        P256dh = "p256dh",
        Auth = "auth",
        Enabled = enabled,
    };

    [Fact]
    public async Task NotifyUserAsync_SendsOnlyToEnabledSubscriptions()
    {
        var userId = Guid.NewGuid();
        var enabledSubscription = MakeSubscription(userId, enabled: true);
        var disabledSubscription = MakeSubscription(userId, enabled: false);
        Store.GetForUserAsync(userId, Arg.Any<CancellationToken>()).Returns([enabledSubscription, disabledSubscription]);
        Sender.SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>()).Returns(PushSendResult.Sent);

        await CreateNotifier().NotifyUserAsync(userId, new PushPayload("Title", "Body"));

        await Sender.Received(1).SendAsync(enabledSubscription, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await Sender.DidNotReceive().SendAsync(disabledSubscription, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyUserAsync_IsANoOp_WhenTheUserHasNoEnabledSubscriptions()
    {
        var userId = Guid.NewGuid();
        Store.GetForUserAsync(userId, Arg.Any<CancellationToken>()).Returns([MakeSubscription(userId, enabled: false)]);

        await CreateNotifier().NotifyUserAsync(userId, new PushPayload("Title", "Body"));

        await Sender.DidNotReceive().SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAllAsync_SendsToEveryEnabledSubscription_AcrossEveryUser()
    {
        var firstUserSubscription = MakeSubscription(Guid.NewGuid());
        var secondUserSubscription = MakeSubscription(Guid.NewGuid());
        Store.GetAllAsync(Arg.Any<CancellationToken>()).Returns([firstUserSubscription, secondUserSubscription]);
        Sender.SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>()).Returns(PushSendResult.Sent);

        await CreateNotifier().NotifyAllAsync(new PushPayload("Title", "Body"));

        await Sender.Received(1).SendAsync(firstUserSubscription, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await Sender.Received(1).SendAsync(secondUserSubscription, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAllAsync_RemovesTheSubscription_WhenTheSenderReportsItGone()
    {
        var goneSubscription = MakeSubscription(Guid.NewGuid());
        Store.GetAllAsync(Arg.Any<CancellationToken>()).Returns([goneSubscription]);
        Sender.SendAsync(goneSubscription, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>()).Returns(PushSendResult.SubscriptionGone);

        await CreateNotifier().NotifyAllAsync(new PushPayload("Title", "Body"));

        await Store.Received(1).RemoveAsync(goneSubscription.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAllAsync_DoesNotRemoveTheSubscription_WhenTheSendJustFailed()
    {
        var failingSubscription = MakeSubscription(Guid.NewGuid());
        Store.GetAllAsync(Arg.Any<CancellationToken>()).Returns([failingSubscription]);
        Sender.SendAsync(failingSubscription, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>()).Returns(PushSendResult.Failed);

        await CreateNotifier().NotifyAllAsync(new PushPayload("Title", "Body"));

        await Store.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAllAsync_StillDeliversToTheOtherSubscriptions_WhenOneSendThrows()
    {
        var throwingSubscription = MakeSubscription(Guid.NewGuid());
        var healthySubscription = MakeSubscription(Guid.NewGuid());
        Store.GetAllAsync(Arg.Any<CancellationToken>()).Returns([throwingSubscription, healthySubscription]);
        Sender.SendAsync(throwingSubscription, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PushSendResult>(new InvalidOperationException("simulated send failure")));
        Sender.SendAsync(healthySubscription, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>()).Returns(PushSendResult.Sent);

        var exception = await Record.ExceptionAsync(() => CreateNotifier().NotifyAllAsync(new PushPayload("Title", "Body")));

        Assert.Null(exception);
        await Sender.Received(1).SendAsync(healthySubscription, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAllAsync_NeverRunsMoreThanEightSendsAtOnce()
    {
        var subscriptions = Enumerable.Range(0, 24).Select(_ => MakeSubscription(Guid.NewGuid())).ToList();
        Store.GetAllAsync(Arg.Any<CancellationToken>()).Returns(subscriptions);

        var concurrentSends = 0;
        var maxObservedConcurrentSends = 0;
        var gate = new object();

        Sender.SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                lock (gate)
                {
                    concurrentSends++;
                    maxObservedConcurrentSends = Math.Max(maxObservedConcurrentSends, concurrentSends);
                }

                await Task.Delay(20, callInfo.ArgAt<CancellationToken>(2));

                lock (gate)
                    concurrentSends--;

                return PushSendResult.Sent;
            });

        await CreateNotifier().NotifyAllAsync(new PushPayload("Title", "Body"));

        Assert.True(maxObservedConcurrentSends <= 8, $"Observed {maxObservedConcurrentSends} concurrent sends, expected at most 8.");
        Assert.True(maxObservedConcurrentSends > 1, "Expected sends to actually overlap for this assertion to be meaningful.");
    }
}
