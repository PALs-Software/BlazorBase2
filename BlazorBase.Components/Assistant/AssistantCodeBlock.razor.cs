using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Assistant;

/// <summary>A fenced code block of an answer with a button that copies the code to the clipboard.</summary>
public sealed partial class AssistantCodeBlock(IJSRuntime jsRuntime, IStringLocalizer<BaseAssistantPanel> localizer) : ComponentBase, IDisposable
{
    #region Injects

    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly IStringLocalizer<BaseAssistantPanel> Localizer = localizer;

    #endregion

    /// <summary>The code; rendered as encoded text.</summary>
    [Parameter]
    public string Code { get; set; } = string.Empty;

    /// <summary>The language named after the fence.</summary>
    [Parameter]
    public string? Language { get; set; }

    #region Members

    private const int CopiedMilliseconds = 1500;

    private bool IsCopied;
    private bool IsDisposed;

    #endregion

    private string? LanguageClass => Language is not null && SafeLanguage().IsMatch(Language) ? "language-" + Language : null;

    private Icon CopyIcon => IsCopied ? new Size16.Checkmark() : new Size16.Copy();

    private async Task CopyAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", Code);
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException or TaskCanceledException)
        {
            return;
        }

        IsCopied = true;
        _ = ResetCopiedAsync();
    }

    private async Task ResetCopiedAsync()
    {
        await Task.Delay(CopiedMilliseconds);

        if (IsDisposed)
            return;

        IsCopied = false;
        await InvokeAsync(StateHasChanged);
    }

    [GeneratedRegex(@"\A[A-Za-z0-9_+#.\-]{1,30}\z")]
    private static partial Regex SafeLanguage();

    /// <inheritdoc/>
    public void Dispose() => IsDisposed = true;
}
