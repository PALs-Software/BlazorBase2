using Microsoft.JSInterop;

namespace BlazorBase.Dashboard;

/// <summary>
/// Wrapper around the dashboard JavaScript module: tab visibility, reduced motion, the browser's UTC offset and the chart
/// colours of the current theme. Every call is fail-soft, because a missing or disconnected browser must not break a part.
/// </summary>
public sealed class DashboardInterop(IJSRuntime jsRuntime) : IAsyncDisposable, IDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> ModuleTask =
        new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorBase.Dashboard/js/dashboard.js").AsTask());

    /// <summary>
    /// True when the user asked the system for less motion.
    /// </summary>
    public async ValueTask<bool> PrefersReducedMotionAsync() => await TryInvokeAsync<bool>("prefersReducedMotion");

    /// <summary>
    /// True when the browser tab is visible; true as well when that cannot be told.
    /// </summary>
    public async ValueTask<bool> IsTabVisibleAsync() => await TryInvokeAsync<bool?>("isTabVisible") ?? true;

    /// <summary>
    /// The UTC offset of the browser in minutes, or null when it cannot be read.
    /// </summary>
    public async ValueTask<int?> GetUtcOffsetMinutesAsync() => await TryInvokeAsync<int?>("getUtcOffsetMinutes");

    /// <summary>
    /// The colour tokens of the current theme, resolved for canvas drawing; also applies them as the Chart.js defaults.
    /// Falls back to <see cref="ChartPalette.Fallback"/>.
    /// </summary>
    public async ValueTask<ChartPalette> ReadChartPaletteAsync() => await TryInvokeAsync<ChartPalette>("readChartPalette") ?? ChartPalette.Fallback;

    /// <summary>
    /// Calls <paramref name="onChanged"/> with the new visibility whenever the tab is shown or hidden. Dispose to stop.
    /// </summary>
    public ValueTask<IAsyncDisposable> ObserveVisibilityAsync(Func<bool, Task> onChanged) => ObserveAsync("observeVisibility", onChanged);

    /// <summary>
    /// Calls <paramref name="onChanged"/> whenever the light/dark theme changes. Dispose to stop.
    /// </summary>
    public ValueTask<IAsyncDisposable> ObserveThemeAsync(Func<Task> onChanged) => ObserveAsync("observeTheme", _ => onChanged());

    /// <summary>
    /// Synchronous disposal for containers that cannot dispose asynchronously; the module reference is left to the circuit.
    /// </summary>
    public void Dispose()
    {
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!ModuleTask.IsValueCreated)
            return;

        try
        {
            var module = await ModuleTask.Value;
            await module.DisposeAsync();
        }
        catch (Exception exception) when (IsJsUnavailable(exception))
        {
        }
    }

    private async ValueTask<IAsyncDisposable> ObserveAsync(string function, Func<bool, Task> callback)
    {
        var reference = DotNetObjectReference.Create(new DashboardJsCallback(callback));

        try
        {
            var module = await ModuleTask.Value;
            var id = await module.InvokeAsync<int>(function, reference);
            return new Subscription(ModuleTask.Value, id, reference);
        }
        catch (Exception exception) when (IsJsUnavailable(exception))
        {
            reference.Dispose();
            return Subscription.None;
        }
    }

    private async ValueTask<T?> TryInvokeAsync<T>(string function)
    {
        try
        {
            var module = await ModuleTask.Value;
            return await module.InvokeAsync<T>(function);
        }
        catch (Exception exception) when (IsJsUnavailable(exception))
        {
            return default;
        }
    }

    private static bool IsJsUnavailable(Exception exception) =>
        exception is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException or OperationCanceledException;

    private sealed class Subscription(Task<IJSObjectReference>? moduleTask, int id, IDisposable? reference) : IAsyncDisposable
    {
        public static readonly Subscription None = new(null, 0, null);

        public async ValueTask DisposeAsync()
        {
            reference?.Dispose();

            if (moduleTask is null)
                return;

            try
            {
                var module = await moduleTask;
                await module.InvokeVoidAsync("unobserve", id);
            }
            catch (Exception exception) when (IsJsUnavailable(exception))
            {
            }
        }
    }
}
