using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The search box in the toolbar of a <see cref="BaseList{TModel}"/>. It waits for a pause in typing before it reports
/// the text, so a list does not load again for every key, and reports a cleared box at once.
/// </summary>
public sealed partial class BaseListQuickSearch : ComponentBase, IDisposable
{
    #region Parameters

    /// <summary>
    /// The text the search currently applies. A change here replaces what is typed, unless the user is still typing.
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// The hint shown while the box is empty, and the accessible name of the box.
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// The accessible name and tooltip of the button that empties the box.
    /// </summary>
    [Parameter]
    public string? ClearLabel { get; set; }

    /// <summary>
    /// How long typing has to pause, in milliseconds, before <see cref="OnSearch"/> is raised. Zero or less raises it for
    /// every change.
    /// </summary>
    [Parameter]
    public int DebounceMilliseconds { get; set; } = DefaultDebounceMilliseconds;

    /// <summary>
    /// Raised with the trimmed search text, empty when the search was cleared, whenever it differs from the text raised
    /// before.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnSearch { get; set; }

    #endregion

    /// <summary>
    /// The pause in typing, in milliseconds, after which the search is applied unless a list asks for another one.
    /// </summary>
    public const int DefaultDebounceMilliseconds = 300;

    private string Text { get; set; } = string.Empty;
    private string AppliedText { get; set; } = string.Empty;
    private string? ReceivedValue { get; set; }
    private CancellationTokenSource? PendingDebounce { get; set; }

    private bool HasText => Text.Length > 0;

    protected override void OnParametersSet()
    {
        if (string.Equals(Value, ReceivedValue, StringComparison.Ordinal))
            return;

        ReceivedValue = Value;

        if (PendingDebounce is not null || string.Equals(Value ?? string.Empty, Text.Trim(), StringComparison.Ordinal))
            return;

        Text = Value ?? string.Empty;
        AppliedText = Text.Trim();
    }

    private async Task OnTextChangedAsync(string? text)
    {
        Text = text ?? string.Empty;
        CancelPendingDebounce();

        if (DebounceMilliseconds <= 0)
        {
            await ApplyAsync();
            return;
        }

        var debounce = new CancellationTokenSource();
        PendingDebounce = debounce;

        try
        {
            await Task.Delay(DebounceMilliseconds, debounce.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ReferenceEquals(PendingDebounce, debounce))
            return;

        PendingDebounce = null;
        debounce.Dispose();

        await ApplyAsync();
    }

    private async Task ClearAsync()
    {
        Text = string.Empty;
        CancelPendingDebounce();
        await ApplyAsync();
    }

    private async Task ApplyAsync()
    {
        var text = Text.Trim();

        if (string.Equals(text, AppliedText, StringComparison.Ordinal))
            return;

        AppliedText = text;
        await OnSearch.InvokeAsync(text);
    }

    private void CancelPendingDebounce()
    {
        var debounce = PendingDebounce;
        PendingDebounce = null;

        if (debounce is null)
            return;

        debounce.Cancel();
        debounce.Dispose();
    }

    public void Dispose() => CancelPendingDebounce();
}
