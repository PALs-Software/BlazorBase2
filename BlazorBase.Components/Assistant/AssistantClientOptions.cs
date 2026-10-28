namespace BlazorBase.Components.Assistant;

/// <summary>Settings of the assistant: where it talks to and how the panel presents it.</summary>
public sealed class AssistantClientOptions
{
    /// <summary>The key of the website, sent as <see cref="AssistantPageContext.SiteId"/>.</summary>
    public string? SiteId { get; set; }

    /// <summary>The URL the messages are posted to; the response is a <c>text/event-stream</c>.</summary>
    public string Endpoint { get; set; } = "/api/agenthub/chat";

    /// <summary>The URL feedback is posted to; null uses <c>feedback</c> next to <see cref="Endpoint"/>.</summary>
    public string? FeedbackEndpoint { get; set; }

    /// <summary>The URL files are uploaded to; null uses <c>files</c> next to <see cref="Endpoint"/>.</summary>
    public string? UploadEndpoint { get; set; }

    /// <summary>
    /// The URL under which <c>{id}/confirm</c> and <c>{id}/decline</c> of a pending action are posted; null uses
    /// <c>actions</c> next to <see cref="Endpoint"/>.
    /// </summary>
    public string? ActionsEndpoint { get; set; }

    /// <summary>The name shown in the header of the panel and on the messages; null uses the localized default.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The URL of the avatar image; null shows the generic icon.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>The text of the empty panel; null uses the localized default.</summary>
    public string? WelcomeText { get; set; }

    /// <summary>Questions offered as buttons while the conversation is empty.</summary>
    public IList<string> Suggestions { get; set; } = [];

    /// <summary>Shows the attach button and sends uploads along with the message.</summary>
    public bool UploadEnabled { get; set; }

    /// <summary>The largest file the panel accepts, in bytes.</summary>
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>The most bytes the serialized <see cref="AssistantPageContext"/> may take; values are shortened or dropped to fit.</summary>
    public int MaxContextBytes { get; set; } = 4096;

    /// <summary>Called before every request of <see cref="SseAssistantChatClient"/>, for example to add an anti-forgery header.</summary>
    public Func<HttpRequestMessage, CancellationToken, ValueTask>? PrepareRequestAsync { get; set; }

    internal string ResolveFeedbackEndpoint() => FeedbackEndpoint ?? ResolveSibling("feedback");

    internal string ResolveUploadEndpoint() => UploadEndpoint ?? ResolveSibling("files");

    internal string ResolveActionsEndpoint() => ActionsEndpoint ?? ResolveSibling("actions");

    private string ResolveSibling(string name)
    {
        var path = Endpoint.TrimEnd('/');
        var lastSlash = path.LastIndexOf('/');
        return lastSlash < 0 ? name : string.Concat(path.AsSpan(0, lastSlash + 1), name);
    }
}
