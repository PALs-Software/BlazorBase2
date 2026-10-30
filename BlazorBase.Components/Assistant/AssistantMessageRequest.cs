namespace BlazorBase.Components.Assistant;

/// <summary>A message the user sends to the assistant.</summary>
public sealed record AssistantMessageRequest
{
    /// <summary>The conversation the message belongs to; null starts a new one.</summary>
    public string? ConversationId { get; init; }

    /// <summary>The text the user typed.</summary>
    public required string Text { get; init; }

    /// <summary>The files uploaded for this message.</summary>
    public IReadOnlyList<AssistantAttachment> Attachments { get; init; } = [];

    /// <summary>Hints about the page the user is on; untrusted, the server adds what it knows itself.</summary>
    public AssistantPageContext? Context { get; init; }
}

/// <summary>A file the user attached to a message; the bytes were uploaded before and are referred to by id.</summary>
/// <param name="Id">The id the upload returned.</param>
/// <param name="FileName">The file name.</param>
/// <param name="ContentType">The media type.</param>
/// <param name="Size">The size in bytes.</param>
public sealed record AssistantAttachment(string Id, string FileName, string ContentType, long Size);

/// <summary>The user's rating of an answer.</summary>
public sealed record AssistantFeedback
{
    /// <summary>The conversation of the answer.</summary>
    public string? ConversationId { get; init; }

    /// <summary>The id of the answer: the server's id when it sent one, otherwise the id the panel generated.</summary>
    public required string MessageId { get; init; }

    /// <summary>Thumbs up or down.</summary>
    public required AssistantRating Rating { get; init; }
}

/// <summary>A rating of an answer.</summary>
public enum AssistantRating
{
    /// <summary>The answer helped.</summary>
    Up,

    /// <summary>The answer did not help.</summary>
    Down
}

/// <summary>The outcome of confirming or declining a pending action.</summary>
/// <param name="Succeeded">Whether the action ran (confirm) or was dropped (decline).</param>
/// <param name="Message">The text for the user; null shows none.</param>
public sealed record AssistantActionResult(bool Succeeded, string? Message = null);
