namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock and timers only move when a test says so.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> Timers = [];

    private DateTimeOffset Now = new(2026, 10, 23, 9, 24, 0, TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.Utc;

    public int ActiveTimers => Timers.Count(timer => !timer.IsDisposed);

    public override DateTimeOffset GetUtcNow() => Now;

    public void SetUtcNow(DateTimeOffset value) => Now = value;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, Now + dueTime, period);
        Timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan span)
    {
        var target = Now + span;

        while (true)
        {
            var next = Timers.Where(timer => timer.IsDue(target)).OrderBy(timer => timer.NextDue).FirstOrDefault();

            if (next is null)
                break;

            Now = next.NextDue;
            next.Fire();
        }

        Now = target;
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset firstDue, TimeSpan period) : ITimer
    {
        public bool IsDisposed { get; private set; }

        public DateTimeOffset NextDue { get; private set; } = firstDue;

        public bool IsDue(DateTimeOffset target) => !IsDisposed && period > TimeSpan.Zero && NextDue <= target;

        public void Fire()
        {
            NextDue += period;
            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan newPeriod) => true;

        public void Dispose() => IsDisposed = true;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
