namespace BlazorBase.Components.Assistant;

/// <summary>The panel's connection to the assistant backend.</summary>
public interface IAssistantChatClient
{
    /// <summary>Sends a message and streams the events of the answer; cancelling the token stops the answer.</summary>
    IAsyncEnumerable<AssistantStreamEvent> SendAsync(AssistantMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sends the user's rating of an answer.</summary>
    Task SendFeedbackAsync(AssistantFeedback feedback, CancellationToken cancellationToken = default);

    /// <summary>Uploads a file and returns the attachment that refers to it.</summary>
    Task<AssistantAttachment> UploadAsync(string fileName, string contentType, Stream content, string? conversationId, CancellationToken cancellationToken = default);

    /// <summary>Confirms a pending action; the server runs it.</summary>
    Task<AssistantActionResult> ConfirmActionAsync(string actionId, string? conversationId, CancellationToken cancellationToken = default);

    /// <summary>Declines a pending action; the server drops it.</summary>
    Task<AssistantActionResult> DeclineActionAsync(string actionId, string? conversationId, CancellationToken cancellationToken = default);
}
