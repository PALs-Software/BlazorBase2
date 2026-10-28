using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorBase.Components.Diagnostics;

/// <summary>
/// An <see cref="ErrorBoundary"/> that records every unhandled exception of its content in the
/// <see cref="IClientDiagnostics"/> buffer, so an issue report can mention it. Use it in place of
/// <c>ErrorBoundary</c>; it behaves identically otherwise and also works without registered diagnostics.
/// </summary>
public sealed class DiagnosticsErrorBoundary : ErrorBoundary
{
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    /// <inheritdoc />
    protected override Task OnErrorAsync(Exception exception)
    {
        (ServiceProvider.GetService(typeof(IClientDiagnostics)) as IClientDiagnostics)?.RecordError("ErrorBoundary", exception);

        return base.OnErrorAsync(exception);
    }
}
