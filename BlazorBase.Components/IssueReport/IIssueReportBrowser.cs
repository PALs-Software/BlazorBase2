using Microsoft.JSInterop;

namespace BlazorBase.Components.IssueReport;

/// <summary>The browser features the issue report needs.</summary>
public interface IIssueReportBrowser
{
    /// <summary>The browser's user agent string; empty when it cannot be read (e.g. during prerendering).</summary>
    ValueTask<string> GetUserAgentAsync();

    /// <summary>Copies text to the clipboard; false when the browser refused.</summary>
    ValueTask<bool> CopyTextAsync(string text);

    /// <summary>
    /// Opens the link in the default mail program. The result is true when the browser window lost focus within a few
    /// seconds, which is how an opened mail program shows; false means it probably did not open.
    /// </summary>
    ValueTask<bool> OpenMailtoAsync(string uri);
}

/// <summary>JavaScript implementation of <see cref="IIssueReportBrowser"/>.</summary>
public sealed class IssueReportBrowser(IJSRuntime jsRuntime) : IIssueReportBrowser, IAsyncDisposable
{
    private const int OpenWaitMilliseconds = 2500;

    #region Injects

    private readonly IJSRuntime JsRuntime = jsRuntime;

    #endregion

    #region Members

    private IJSObjectReference? Module;

    #endregion

    /// <inheritdoc />
    public async ValueTask<string> GetUserAgentAsync()
    {
        try
        {
            var module = await GetModuleAsync();

            return await module.InvokeAsync<string>("getUserAgent");
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSException or JSDisconnectedException)
        {
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> CopyTextAsync(string text)
    {
        try
        {
            var module = await GetModuleAsync();

            return await module.InvokeAsync<bool>("copyText", text);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSException or JSDisconnectedException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> OpenMailtoAsync(string uri)
    {
        try
        {
            var module = await GetModuleAsync();

            return await module.InvokeAsync<bool>("openMailto", uri, OpenWaitMilliseconds);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSException or JSDisconnectedException)
        {
            return false;
        }
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync() =>
        Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorBase.Components/js/issueReport.js");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Module is null)
            return;

        try
        {
            await Module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
