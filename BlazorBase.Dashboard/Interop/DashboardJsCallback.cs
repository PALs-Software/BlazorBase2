using Microsoft.JSInterop;

namespace BlazorBase.Dashboard;

/// <summary>
/// Receives a call from the dashboard script and hands it to a delegate; the target of the <c>DotNetObjectReference</c>.
/// </summary>
/// <param name="callback">Invoked with the value the script passes (tab visible, theme changed).</param>
public sealed class DashboardJsCallback(Func<bool, Task> callback)
{
    /// <summary>
    /// Called by the script.
    /// </summary>
    [JSInvokable]
    public Task Invoke(bool value) => callback(value);
}
