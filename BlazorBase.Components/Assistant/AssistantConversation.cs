using System.Text;

namespace BlazorBase.Components.Assistant;

/// <summary>The messages of one conversation and its id; the panel renders it.</summary>
public sealed class AssistantConversation
{
    private readonly List<AssistantMessage> MessageList = [];

    /// <summary>The id the server gave the conversation; null until the first answer started.</summary>
    public string? Id { get; set; }

    /// <summary>The messages in the order they were sent.</summary>
    public IReadOnlyList<AssistantMessage> Messages => MessageList;

    /// <summary>Adds a message at the end.</summary>
    public void Add(AssistantMessage message) => MessageList.Add(message);

    /// <summary>Forgets the id and all messages.</summary>
    public void Reset()
    {
        Id = null;
        MessageList.Clear();
    }
}

/// <summary>Who wrote a message.</summary>
public enum AssistantMessageRole
{
    /// <summary>The user.</summary>
    User,

    /// <summary>The assistant.</summary>
    Assistant,

    /// <summary>The panel itself, for example the result of a confirmed action.</summary>
    Notice
}

/// <summary>One message of a conversation; an answer fills while it streams.</summary>
public sealed class AssistantMessage
{
    private readonly StringBuilder TextBuilder = new();
    private string? CachedText;

    /// <summary>Creates a message of the given author.</summary>
    public AssistantMessage(AssistantMessageRole role, string? text = null)
    {
        Role = role;

        if (!string.IsNullOrEmpty(text))
            Append(text);
    }

    /// <summary>The id the panel generated.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>The id the server gave the answer; null until it finished.</summary>
    public string? ServerId { get; set; }

    /// <summary>Who wrote the message.</summary>
    public AssistantMessageRole Role { get; }

    /// <summary>The Markdown text received so far.</summary>
    public string Text => CachedText ??= TextBuilder.ToString();

    /// <summary>The sources of the answer.</summary>
    public List<AssistantCitation> Citations { get; } = [];

    /// <summary>The files sent with a user message.</summary>
    public IReadOnlyList<AssistantAttachment> Attachments { get; init; } = [];

    /// <summary>The write waiting for confirmation, when the answer prepared one.</summary>
    public AssistantPendingAction? PendingAction { get; set; }

    /// <summary>The label of the tool call that runs now; null when none runs.</summary>
    public string? ToolStatus { get; set; }

    /// <summary>The id of the tool call <see cref="ToolStatus"/> belongs to.</summary>
    public string? ToolId { get; set; }

    /// <summary>True while the answer is still arriving.</summary>
    public bool IsStreaming { get; set; }

    /// <summary>True when the user stopped the answer.</summary>
    public bool IsStopped { get; set; }

    /// <summary>The error that ended the answer.</summary>
    public AssistantErrorEvent? Error { get; set; }

    /// <summary>The user's rating of the answer.</summary>
    public AssistantRating? Rating { get; set; }

    /// <summary>The id feedback refers to.</summary>
    public string FeedbackId => ServerId ?? Id;

    /// <summary>Appends a piece of text.</summary>
    public void Append(string text)
    {
        TextBuilder.Append(text);
        CachedText = null;
    }
}

/// <summary>A source of an answer.</summary>
/// <param name="Title">The display title.</param>
/// <param name="Url">The target, when it has one.</param>
/// <param name="Snippet">An excerpt, shown as tooltip.</param>
public sealed record AssistantCitation(string Title, string? Url, string? Snippet);

/// <summary>A write the user confirms or declines.</summary>
public sealed class AssistantPendingAction
{
    /// <summary>Creates the action from its event.</summary>
    public AssistantPendingAction(AssistantPendingActionEvent source)
    {
        Id = source.Id;
        Summary = source.Summary;
        Parameters = source.Parameters ?? new Dictionary<string, string>();
    }

    /// <summary>The id the confirm and decline calls refer to.</summary>
    public string Id { get; }

    /// <summary>What happens, in a sentence.</summary>
    public string Summary { get; }

    /// <summary>The values the action runs with.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>Where the action stands.</summary>
    public AssistantActionState State { get; set; } = AssistantActionState.Pending;

    /// <summary>The text the server returned for the outcome.</summary>
    public string? ResultMessage { get; set; }
}

/// <summary>Where a pending action stands.</summary>
public enum AssistantActionState
{
    /// <summary>Waiting for the user.</summary>
    Pending,

    /// <summary>The server is processing the decision.</summary>
    Working,

    /// <summary>The user confirmed and the action ran.</summary>
    Confirmed,

    /// <summary>The user declined.</summary>
    Declined,

    /// <summary>The decision did not go through.</summary>
    Failed
}
