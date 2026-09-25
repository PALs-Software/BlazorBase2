using BlazorBase.Speech.Server.Configuration;
using BlazorBase.Speech.Server.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Speech.Server.Test.Services;

public sealed class SpeechRequestGateTests
{
    private static SpeechRequestGate CreateGate(int limit)
        => new(Options.Create(new SpeechServerOptions { MaxConcurrentRequestsPerUser = limit }));

    [Fact]
    public void TryEnter_RefusesBeyondTheLimit_PerUser()
    {
        var gate = CreateGate(2);

        using var first = gate.TryEnter("alice");
        using var second = gate.TryEnter("alice");
        using var third = gate.TryEnter("alice");
        using var otherUser = gate.TryEnter("bob");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(third);
        Assert.NotNull(otherUser);
    }

    [Fact]
    public void DisposingALease_FreesExactlyOneSlot()
    {
        var gate = CreateGate(1);
        var lease = gate.TryEnter("alice");

        lease!.Dispose();
        lease.Dispose();
        using var next = gate.TryEnter("alice");
        using var overflow = gate.TryEnter("alice");

        Assert.NotNull(next);
        Assert.Null(overflow);
    }
}
