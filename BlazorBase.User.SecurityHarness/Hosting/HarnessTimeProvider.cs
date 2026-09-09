namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>
/// A <see cref="TimeProvider"/> a probe can move forward on demand, so the harness can observe
/// whether the framework under test actually resolves its clock from dependency injection rather
/// than reading <see cref="DateTimeOffset.UtcNow"/> directly. Delegates <see cref="GetTimestamp"/>,
/// <see cref="TimestampFrequency"/> and <see cref="CreateTimer"/> to <see cref="TimeProvider.System"/>;
/// only <see cref="GetUtcNow"/> carries the offset. <see cref="Advance(TimeSpan)"/> is safe to call
/// from another thread while the host serves requests.
/// </summary>
internal sealed class HarnessTimeProvider : TimeProvider
{
    private long _offsetTicks;

    public override DateTimeOffset GetUtcNow() =>
        TimeProvider.System.GetUtcNow().AddTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan offset) =>
        Interlocked.Add(ref _offsetTicks, offset.Ticks);

    public override long GetTimestamp() => TimeProvider.System.GetTimestamp();

    public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        TimeProvider.System.CreateTimer(callback, state, dueTime, period);
}
