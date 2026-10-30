using System.Runtime.CompilerServices;

namespace BlazorBase.Components.Assistant;

/// <summary>
/// A deterministic <see cref="IAssistantChatClient"/> without a backend, for tests and for developing the UI: every
/// message is answered with the events queued by <see cref="Reply"/>, or with a canned answer that echoes the question.
/// </summary>
/// <remarks>Everything the client receives is recorded in <see cref="Requests"/>, <see cref="Feedbacks"/>, <see cref="ConfirmedActions"/> and <see cref="DeclinedActions"/>.</remarks>
public sealed class ScriptedAssistantChatClient : IAssistantChatClient
{
    private readonly Queue<IReadOnlyList<AssistantStreamEvent>> Replies = new();

    /// <summary>The time between two events; zero by default.</summary>
    public TimeSpan EventDelay { get; set; } = TimeSpan.Zero;

    /// <summary>The messages received.</summary>
    public List<AssistantMessageRequest> Requests { get; } = [];

    /// <summary>The ratings received.</summary>
    public List<AssistantFeedback> Feedbacks { get; } = [];

    /// <summary>The files received, as file names.</summary>
    public List<string> Uploads { get; } = [];

    /// <summary>The ids of the confirmed actions.</summary>
    public List<string> ConfirmedActions { get; } = [];

    /// <summary>The ids of the declined actions.</summary>
    public List<string> DeclinedActions { get; } = [];

    /// <summary>The result of <see cref="ConfirmActionAsync"/>.</summary>
    public AssistantActionResult ConfirmResult { get; set; } = new(true, "Done.");

    /// <summary>The result of <see cref="DeclineActionAsync"/>.</summary>
    public AssistantActionResult DeclineResult { get; set; } = new(true, "Dropped.");

    /// <summary>Queues the events the next message is answered with.</summary>
    public ScriptedAssistantChatClient Reply(params AssistantStreamEvent[] events)
    {
        Replies.Enqueue(events);
        return this;
    }

    /// <summary>Queues a plain answer: conversation start, the text in word-sized pieces, finish.</summary>
    public ScriptedAssistantChatClient ReplyWithText(string text) => Reply(TextAnswer(text));

    /// <inheritdoc/>
    public async IAsyncEnumerable<AssistantStreamEvent> SendAsync(AssistantMessageRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Add(request);

        var events = Replies.Count > 0 ? Replies.Dequeue() : DefaultAnswer(request);

        foreach (var streamEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (EventDelay > TimeSpan.Zero)
                await Task.Delay(EventDelay, cancellationToken);

            yield return streamEvent;
        }
    }

    /// <inheritdoc/>
    public Task SendFeedbackAsync(AssistantFeedback feedback, CancellationToken cancellationToken = default)
    {
        Feedbacks.Add(feedback);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<AssistantAttachment> UploadAsync(string fileName, string contentType, Stream content, string? conversationId, CancellationToken cancellationToken = default)
    {
        Uploads.Add(fileName);
        return Task.FromResult(new AssistantAttachment($"upload-{Uploads.Count}", fileName, contentType, content.CanSeek ? content.Length : 0));
    }

    /// <inheritdoc/>
    public Task<AssistantActionResult> ConfirmActionAsync(string actionId, string? conversationId, CancellationToken cancellationToken = default)
    {
        ConfirmedActions.Add(actionId);
        return Task.FromResult(ConfirmResult);
    }

    /// <inheritdoc/>
    public Task<AssistantActionResult> DeclineActionAsync(string actionId, string? conversationId, CancellationToken cancellationToken = default)
    {
        DeclinedActions.Add(actionId);
        return Task.FromResult(DeclineResult);
    }

    private static AssistantStreamEvent[] DefaultAnswer(AssistantMessageRequest request) =>
    [
        new AssistantConversationStartedEvent(request.ConversationId ?? "scripted-conversation"),
        new AssistantToolStatusEvent("search", "Searching documentation…"),
        new AssistantToolStatusEvent("search", "Searching documentation…", AssistantToolState.Completed),
        .. TextPieces($"You asked: *{request.Text}*"),
        new AssistantCitationEvent("Scripted source", "https://example.com/source"),
        new AssistantFinishedEvent("scripted-message")
    ];

    private static AssistantStreamEvent[] TextAnswer(string text) =>
    [
        new AssistantConversationStartedEvent("scripted-conversation"),
        .. TextPieces(text),
        new AssistantFinishedEvent("scripted-message")
    ];

    private static IEnumerable<AssistantStreamEvent> TextPieces(string text) =>
        text.Split(' ').Select((word, index) => (AssistantStreamEvent)new AssistantTextDeltaEvent(index == 0 ? word : " " + word));
}
