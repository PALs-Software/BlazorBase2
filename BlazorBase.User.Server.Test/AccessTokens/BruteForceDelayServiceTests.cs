using BlazorBase.User.Server.AccessTokens.BruteForce;
using BlazorBase.User.Server.AccessTokens;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.AccessTokens;

public class BruteForceDelayServiceTests
{
    private static BruteForceDelayService CreateService(int baseDelayMs = 200, int maxDelayMs = 10000) =>
        new(Options.Create(new AccessTokenSettings { BruteForceBaseDelayMs = baseDelayMs, BruteForceMaxDelayMs = maxDelayMs }));

    [Fact]
    public void No_failures_means_no_delay()
    {
        Assert.Equal(0, CreateService().CalculateDelayMilliseconds(0));
        Assert.Equal(0, CreateService().CalculateDelayMilliseconds(-1));
    }

    [Theory]
    [InlineData(1, 200)]
    [InlineData(2, 400)]
    [InlineData(3, 800)]
    [InlineData(4, 1600)]
    public void The_delay_doubles_with_each_failure(int failures, int expectedDelay)
    {
        Assert.Equal(expectedDelay, CreateService().CalculateDelayMilliseconds(failures));
    }

    [Fact]
    public void The_delay_is_capped_at_the_configured_maximum()
    {
        Assert.Equal(10000, CreateService().CalculateDelayMilliseconds(20));
    }

    /// <summary>
    /// The shift is clamped before it is applied. Without that, a client with enough recorded
    /// failures shifts a 64-bit value past its width, and the delay collapses back towards zero -
    /// the brake would release precisely for the most persistent attacker.
    /// </summary>
    [Fact]
    public void A_very_large_failure_count_stays_capped_instead_of_wrapping_to_zero()
    {
        Assert.Equal(10000, CreateService().CalculateDelayMilliseconds(int.MaxValue));
    }

    [Fact]
    public void A_success_clears_the_recorded_failures()
    {
        var service = CreateService();

        service.RegisterFailure("10.0.0.1");
        service.RegisterFailure("10.0.0.1");
        service.RegisterSuccess("10.0.0.1");

        Assert.Equal(0, service.CalculateDelayMilliseconds(0));
    }

    [Fact]
    public async Task An_unknown_client_is_not_delayed()
    {
        var service = CreateService(baseDelayMs: 5000, maxDelayMs: 5000);

        var waiting = service.GetDelayAsync("10.0.0.99");

        Assert.True(waiting.IsCompleted);
        await waiting;
    }
}
