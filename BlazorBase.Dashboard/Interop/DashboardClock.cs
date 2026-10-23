namespace BlazorBase.Dashboard;

/// <summary>
/// The time of day the user lives in. A server-rendered app only knows the server's time zone, so the first component
/// that renders asks the browser for its UTC offset once; until then, and when the browser cannot answer, the
/// <see cref="TimeProvider"/>'s local time zone applies.
/// </summary>
public sealed class DashboardClock(TimeProvider timeProvider, DashboardInterop interop)
{
    private TimeSpan? BrowserOffset;

    /// <summary>
    /// The current time in the user's time zone.
    /// </summary>
    public DateTimeOffset LocalNow => ToLocal(timeProvider.GetUtcNow());

    /// <summary>
    /// Converts <paramref name="instant"/> to the user's time zone.
    /// </summary>
    public DateTimeOffset ToLocal(DateTimeOffset instant) =>
        BrowserOffset is { } offset ? instant.ToOffset(offset) : TimeZoneInfo.ConvertTime(instant, timeProvider.LocalTimeZone);

    /// <summary>
    /// Reads the browser's UTC offset once. Returns true when it changed what <see cref="LocalNow"/> reports, so the caller can render again.
    /// </summary>
    public async Task<bool> EnsureInitializedAsync()
    {
        if (BrowserOffset is not null)
            return false;

        var before = LocalNow.Offset;
        var minutes = await interop.GetUtcOffsetMinutesAsync();

        if (minutes is not { } value)
            return false;

        BrowserOffset = TimeSpan.FromMinutes(value);
        return BrowserOffset != before;
    }
}
