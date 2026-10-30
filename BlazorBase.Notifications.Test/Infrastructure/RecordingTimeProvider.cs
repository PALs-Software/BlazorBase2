using System.Threading;

namespace BlazorBase.Notifications.Test.Infrastructure;

/// <summary>Records the due time and period passed to the single timer it creates, without actually firing it.</summary>
public sealed class RecordingTimeProvider : TimeProvider
{
    public TimeSpan? LastDueTime { get; private set; }

    public TimeSpan? LastPeriod { get; private set; }

    public int CreateTimerCallCount { get; private set; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        LastDueTime = dueTime;
        LastPeriod = period;
        CreateTimerCallCount++;
        return new NoOpTimer();
    }

    private sealed class NoOpTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
