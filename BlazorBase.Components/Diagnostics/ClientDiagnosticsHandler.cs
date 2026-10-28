using System.Net;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Diagnostics;

/// <summary>
/// A <see cref="DelegatingHandler"/> that remembers the correlation id of every response and records failed requests
/// (HTTP 5xx and transport errors) without their query string. Attach it with
/// <c>AddHttpMessageHandler&lt;ClientDiagnosticsHandler&gt;()</c>; it needs the diagnostics shared by the whole process
/// (WebAssembly), because a handler runs in its own dependency injection scope.
/// </summary>
public sealed class ClientDiagnosticsHandler(IClientDiagnostics diagnostics, IOptions<ClientDiagnosticsOptions> options) : DelegatingHandler
{
    #region Injects

    private readonly IClientDiagnostics Diagnostics = diagnostics;
    private readonly ClientDiagnosticsOptions Options = options.Value;

    #endregion

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            Diagnostics.RecordError("Http", $"{request.Method} {Describe(request.RequestUri)} failed: {exception.Message}");
            throw;
        }

        Diagnostics.RecordCorrelationId(ReadCorrelationId(response));
        if (response.StatusCode >= HttpStatusCode.InternalServerError)
            Diagnostics.RecordError("Http", $"{request.Method} {Describe(request.RequestUri)} returned {(int)response.StatusCode}");

        return response;
    }

    private string? ReadCorrelationId(HttpResponseMessage response)
    {
        foreach (var header in Options.CorrelationHeaders)
        {
            if (response.Headers.TryGetValues(header, out var values))
                return values.FirstOrDefault();
        }

        return null;
    }

    private static string Describe(Uri? uri) => uri is null ? string.Empty : uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString.Split('?')[0];
}
