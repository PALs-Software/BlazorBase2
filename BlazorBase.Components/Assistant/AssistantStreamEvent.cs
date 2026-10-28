using System.Text.Json.Serialization;

namespace BlazorBase.Components.Assistant;

/// <summary>
/// One event of the answer stream of the assistant. Serialized as JSON with a <c>type</c> discriminator
/// (see <see cref="AssistantJson"/>); on the wire each event is one Server-Sent Events message.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AssistantConversationStartedEvent), AssistantStreamEventTypes.ConversationStarted)]
[JsonDerivedType(typeof(AssistantTextDeltaEvent), AssistantStreamEventTypes.TextDelta)]
[JsonDerivedType(typeof(AssistantToolStatusEvent), AssistantStreamEventTypes.ToolStatus)]
[JsonDerivedType(typeof(AssistantCitationEvent), AssistantStreamEventTypes.Citation)]
[JsonDerivedType(typeof(AssistantPendingActionEvent), AssistantStreamEventTypes.PendingAction)]
[JsonDerivedType(typeof(AssistantFinishedEvent), AssistantStreamEventTypes.Finished)]
[JsonDerivedType(typeof(AssistantErrorEvent), AssistantStreamEventTypes.Error)]
public abstract record AssistantStreamEvent;

/// <summary>The values of the <c>type</c> discriminator, which is also the SSE event name.</summary>
public static class AssistantStreamEventTypes
{
    /// <summary>The conversation exists and has an id.</summary>
    public const string ConversationStarted = "conversation_started";

    /// <summary>A piece of the answer text.</summary>
    public const string TextDelta = "text_delta";

    /// <summary>The assistant started or finished a tool call.</summary>
    public const string ToolStatus = "tool_status";

    /// <summary>A source of the answer.</summary>
    public const string Citation = "citation";

    /// <summary>A prepared write that waits for the user's confirmation.</summary>
    public const string PendingAction = "pending_action";

    /// <summary>The answer is complete.</summary>
    public const string Finished = "finished";

    /// <summary>The answer failed.</summary>
    public const string Error = "error";
}

/// <summary>The conversation has an id; sent first for a new conversation.</summary>
/// <param name="ConversationId">The id the client sends with every following message.</param>
public sealed record AssistantConversationStartedEvent(string ConversationId) : AssistantStreamEvent;

/// <summary>A piece of the answer, appended to the text received so far.</summary>
/// <param name="Text">The Markdown fragment.</param>
public sealed record AssistantTextDeltaEvent(string Text) : AssistantStreamEvent;

/// <summary>The progress of a tool call, shown as a status line while it runs.</summary>
/// <param name="ToolId">Identifies the call, so the event that ends it matches the one that started it.</param>
/// <param name="Label">The text for the user, for example "Searching documentation…".</param>
/// <param name="State">Whether the call started, completed or failed.</param>
public sealed record AssistantToolStatusEvent(string ToolId, string Label, AssistantToolState State = AssistantToolState.Started) : AssistantStreamEvent;

/// <summary>A source the answer relies on.</summary>
/// <param name="Title">The display title.</param>
/// <param name="Url">The target; only <c>http</c>, <c>https</c> and relative URLs become links.</param>
/// <param name="Snippet">An optional excerpt, shown as the tooltip.</param>
public sealed record AssistantCitationEvent(string Title, string? Url = null, string? Snippet = null) : AssistantStreamEvent;

/// <summary>A write the assistant prepared; it only happens after the user confirms it.</summary>
/// <param name="Id">The id the confirm and decline calls refer to.</param>
/// <param name="Summary">What happens, in a sentence for the user.</param>
/// <param name="Parameters">The values the action runs with, shown as a list; null shows none.</param>
public sealed record AssistantPendingActionEvent(string Id, string Summary, IReadOnlyDictionary<string, string>? Parameters = null) : AssistantStreamEvent;

/// <summary>The answer is complete.</summary>
/// <param name="MessageId">The id the server gave the answer; feedback refers to it.</param>
/// <param name="InputTokens">The tokens the request consumed, when reported.</param>
/// <param name="OutputTokens">The tokens the answer consumed, when reported.</param>
public sealed record AssistantFinishedEvent(string? MessageId = null, int? InputTokens = null, int? OutputTokens = null) : AssistantStreamEvent;

/// <summary>The answer failed; the stream ends after it.</summary>
/// <param name="Code">A short machine-readable code, for example <c>rate_limited</c>.</param>
/// <param name="Message">The text for the user.</param>
/// <param name="Retryable">Whether sending the message again can succeed.</param>
public sealed record AssistantErrorEvent(string Code, string Message, bool Retryable = false) : AssistantStreamEvent;

/// <summary>The state of a tool call.</summary>
public enum AssistantToolState
{
    /// <summary>The call is running.</summary>
    Started,

    /// <summary>The call finished.</summary>
    Completed,

    /// <summary>The call failed.</summary>
    Failed
}
