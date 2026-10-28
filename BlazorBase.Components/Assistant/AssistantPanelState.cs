namespace BlazorBase.Components.Assistant;

/// <summary>
/// Whether the assistant panel is open, and the conversation it shows. Header actions call <see cref="Toggle"/>;
/// the panel and the buttons listen to <see cref="Changed"/>. The scoped lifetime keeps the conversation while the
/// user moves from page to page.
/// </summary>
public sealed class AssistantPanelState
{
    private bool OpenValue;

    /// <summary>Raised when <see cref="IsOpen"/> changes.</summary>
    public event Action? Changed;

    /// <summary>Whether the panel is shown.</summary>
    public bool IsOpen => OpenValue;

    /// <summary>The conversation the panel shows.</summary>
    public AssistantConversation Conversation { get; } = new();

    /// <summary>The text typed but not yet sent; kept while the panel is closed.</summary>
    public string Draft { get; set; } = string.Empty;

    /// <summary>Shows the panel.</summary>
    public void Open() => SetOpen(true);

    /// <summary>Hides the panel; the conversation stays.</summary>
    public void Close() => SetOpen(false);

    /// <summary>Opens a closed panel and closes an open one.</summary>
    public void Toggle() => SetOpen(!OpenValue);

    private void SetOpen(bool value)
    {
        if (OpenValue == value)
            return;

        OpenValue = value;
        Changed?.Invoke();
    }
}
