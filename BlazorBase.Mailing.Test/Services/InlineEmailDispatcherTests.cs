using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using Xunit;

namespace BlazorBase.Mailing.Test.Services;

/// <summary>
/// Tests for <see cref="InlineEmailDispatcher"/>: unlike the background path, a failing send must
/// propagate to the caller of <see cref="IEmailDispatcher.DispatchAsync"/>.
/// </summary>
public sealed class InlineEmailDispatcherTests
{
    private sealed class ThrowingEmailSender(Exception exception) : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
            throw exception;
    }

    [Fact]
    public async Task DispatchAsync_PropagatesSenderException()
    {
        var expected = new InvalidOperationException("SMTP unreachable");
        var dispatcher = new InlineEmailDispatcher(new ThrowingEmailSender(expected));
        var message = new EmailMessage { To = ["to@example.com"], Subject = "Hi" };

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(message).AsTask());

        Assert.Same(expected, actual);
    }
}
