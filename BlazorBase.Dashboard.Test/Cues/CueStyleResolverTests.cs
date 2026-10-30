using Xunit;

namespace BlazorBase.Dashboard.Test.Cues;

public class CueStyleResolverTests
{
    [Theory]
    [InlineData(0, CueStyle.Favorable)]
    [InlineData(74.99, CueStyle.Favorable)]
    [InlineData(75, CueStyle.Ambiguous)]
    [InlineData(89.99, CueStyle.Ambiguous)]
    [InlineData(90, CueStyle.Unfavorable)]
    [InlineData(250, CueStyle.Unfavorable)]
    public void HigherIsWorse_UsesInclusiveBoundaries(double value, CueStyle expected) =>
        Assert.Equal(expected, CueStyleResolver.Resolve((decimal)value, CueThresholds.HigherIsWorse(ambiguousFrom: 75, unfavorableFrom: 90)));

    [Theory]
    [InlineData(100, CueStyle.Favorable)]
    [InlineData(50.01, CueStyle.Favorable)]
    [InlineData(50, CueStyle.Ambiguous)]
    [InlineData(20.01, CueStyle.Ambiguous)]
    [InlineData(20, CueStyle.Unfavorable)]
    [InlineData(-3, CueStyle.Unfavorable)]
    public void HigherIsBetter_UsesInclusiveBoundariesDownwards(double value, CueStyle expected) =>
        Assert.Equal(expected, CueStyleResolver.Resolve((decimal)value, CueThresholds.HigherIsBetter(ambiguousFrom: 50, unfavorableFrom: 20)));

    [Fact]
    public void EqualBoundaries_SkipAmbiguous_UnfavorableWins()
    {
        var thresholds = CueThresholds.HigherIsWorse(1, 1);

        Assert.Equal(CueStyle.Favorable, CueStyleResolver.Resolve(0, thresholds));
        Assert.Equal(CueStyle.Unfavorable, CueStyleResolver.Resolve(1, thresholds));
    }

    [Fact]
    public void HigherIsWorse_WithBoundariesTheWrongWayRound_Throws() =>
        Assert.Throws<ArgumentException>(() => CueThresholds.HigherIsWorse(90, 75));

    [Fact]
    public void HigherIsBetter_WithBoundariesTheWrongWayRound_Throws() =>
        Assert.Throws<ArgumentException>(() => CueThresholds.HigherIsBetter(20, 50));

    [Fact]
    public void Thresholds_ExposeTheirDirectionAndBoundaries()
    {
        var thresholds = CueThresholds.HigherIsBetter(50, 20);

        Assert.Equal(CueDirection.HigherIsBetter, thresholds.Direction);
        Assert.Equal(50, thresholds.AmbiguousFrom);
        Assert.Equal(20, thresholds.UnfavorableFrom);
    }

    [Fact]
    public void NullThresholds_Throw() =>
        Assert.Throws<ArgumentNullException>(() => CueStyleResolver.Resolve(1, null!));

    [Theory]
    [InlineData(0, CueStyle.None)]
    [InlineData(-1, CueStyle.None)]
    [InlineData(1, CueStyle.Unfavorable)]
    [InlineData(40, CueStyle.Unfavorable)]
    public void ForCount_IsOnlyAStyleAboveZero(double count, CueStyle expected) =>
        Assert.Equal(expected, CueStyleResolver.ForCount((decimal)count, CueStyle.Unfavorable));

    [Fact]
    public void ForCount_CanReportAFavorableZero() =>
        Assert.Equal(CueStyle.Favorable, CueStyleResolver.ForCount(0, CueStyle.Unfavorable, CueStyle.Favorable));
}
