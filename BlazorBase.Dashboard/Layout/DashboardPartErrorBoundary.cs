using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorBase.Dashboard;

/// <summary>
/// The error boundary around one part: logs like <see cref="ErrorBoundary"/> and tells the part frame that the part failed.
/// </summary>
public sealed class DashboardPartErrorBoundary : ErrorBoundary
{
    /// <summary>
    /// Called with the exception that the part threw while rendering or in one of its lifecycle methods.
    /// </summary>
    [Parameter]
    public Action<Exception>? OnFailure { get; set; }

    /// <inheritdoc />
    protected override async Task OnErrorAsync(Exception exception)
    {
        await base.OnErrorAsync(exception);
        OnFailure?.Invoke(exception);
    }
}
