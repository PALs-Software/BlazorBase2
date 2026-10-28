using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Assistant;

/// <summary>
/// The assistant panel: a chat with transcript, streaming answer, sources, feedback and a stop button. On wide
/// screens it is docked at the end edge next to the page and can be resized, on narrower screens it floats over the
/// page, and on phones it is a bottom sheet over a dimmed page.
/// </summary>
/// <remarks>
/// Open and close it through <see cref="AssistantPanelState"/>, typically from a header action. The panel renders
/// nothing while closed and keeps the conversation in the state, so it survives page navigation. Place it once, inside
/// the layout (for example in <c>BaseLayout</c>'s overlay slot); it positions itself below the header and, when the
/// layout has a <c>#base-main</c> element, reserves its width next to it on wide screens. Services come from
/// <see cref="AssistantServiceCollectionExtensions.AddBlazorBaseAssistant(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{AssistantClientOptions}?)"/>.
/// <para>
/// Enter sends the message, Shift+Enter adds a line and Escape closes the panel. Answers are rendered from Markdown
/// with raw HTML escaped; links open with <c>rel="noopener noreferrer"</c>.
/// </para>
/// </remarks>
public sealed partial class BaseAssistantPanel(
    AssistantPanelState state,
    IAssistantChatClient client,
    AssistantContextService contextService,
    IOptions<AssistantClientOptions> options,
    IJSRuntime jsRuntime,
    IStringLocalizer<BaseAssistantPanel> localizer) : ComponentBase, IAsyncDisposable
{
    #region Injects

    private readonly AssistantPanelState State = state;
    private readonly IAssistantChatClient Client = client;
    private readonly AssistantContextService ContextService = contextService;
    private readonly IOptions<AssistantClientOptions> Options = options;
    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly IStringLocalizer<BaseAssistantPanel> Localizer = localizer;

    #endregion

    /// <summary>Id of the panel element, for the button that opens it to point <c>aria-controls</c> at.</summary>
    [Parameter]
    public string PanelId { get; set; } = "base-assistant";

    /// <summary>The name of the assistant; overrides <see cref="AssistantClientOptions.DisplayName"/>.</summary>
    [Parameter]
    public string? DisplayName { get; set; }

    /// <summary>The URL of the avatar image; overrides <see cref="AssistantClientOptions.AvatarUrl"/>.</summary>
    [Parameter]
    public string? AvatarUrl { get; set; }

    /// <summary>Content shown instead of the avatar image or the generic icon.</summary>
    [Parameter]
    public RenderFragment? AvatarContent { get; set; }

    /// <summary>The text of the empty panel; overrides <see cref="AssistantClientOptions.WelcomeText"/>.</summary>
    [Parameter]
    public string? WelcomeText { get; set; }

    #region Members

    private const int MaxMessageLength = 8000;
    private const int MaxFilesPerSelection = 5;
    private const long RenderIntervalMilliseconds = 50;

    private IJSObjectReference? Module;
    private DotNetObjectReference<BaseAssistantPanel>? SelfReference;
    private CancellationTokenSource? AnswerSource;
    private ElementReference PanelElement;
    private ElementReference ResizeElement;
    private ElementReference TranscriptElement;
    private ElementReference ComposerElement;
    private readonly List<AssistantAttachment> PendingAttachments = [];
    private string? StatusText;
    private bool IsAttached;
    private bool IsStreaming;
    private bool IsScrollPending;
    private bool IsScrollForced;
    private bool IsComposerResizePending;
    private bool IsRenderScheduled;
    private bool IsDisposed;

    #endregion

    private AssistantConversation Conversation => State.Conversation;

    private AssistantClientOptions Settings => Options.Value;

    private string TitleId => PanelId + "-title";

    private string ResolvedDisplayName => DisplayName ?? Settings.DisplayName ?? Localizer["Title"].Value;

    private string? ResolvedAvatarUrl => AvatarUrl ?? Settings.AvatarUrl;

    private string ResolvedWelcomeText => WelcomeText ?? Settings.WelcomeText ?? Localizer["Welcome"].Value;

    /// <inheritdoc/>
    protected override void OnInitialized() => State.Changed += OnStateChanged;

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsDisposed)
            return;

        if (!State.IsOpen)
        {
            await DetachAsync();
            return;
        }

        if (!IsAttached)
        {
            Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorBase.Components/js/assistantPanel.js");
            SelfReference ??= DotNetObjectReference.Create(this);
            await Module.InvokeVoidAsync("attach", PanelElement, ResizeElement, TranscriptElement, ComposerElement, SelfReference);
            IsAttached = true;
            await ComposerElement.FocusAsync();
            IsScrollPending = true;
            IsScrollForced = true;
        }

        if (IsComposerResizePending && Module is not null)
        {
            IsComposerResizePending = false;
            await Module.InvokeVoidAsync("resizeComposer");
        }

        if (IsScrollPending && Module is not null)
        {
            var force = IsScrollForced;
            IsScrollPending = false;
            IsScrollForced = false;
            await Module.InvokeVoidAsync("scrollToEnd", force);
        }
    }

    /// <summary>Sends the text of the composer; called by the script when the user presses Enter.</summary>
    [JSInvokable]
    public async Task SubmitFromKeyboardAsync(string text)
    {
        State.Draft = text ?? string.Empty;
        await InvokeAsync(StateHasChanged);
        await SendAsync();
    }

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnKeyDown(KeyboardEventArgs eventArgs)
    {
        if (eventArgs.Key == "Escape")
            State.Close();
    }

    private Task SendTextAsync(string text)
    {
        State.Draft = text;
        return SendAsync();
    }

    private async Task SendAsync()
    {
        var text = State.Draft.Trim();

        if (IsStreaming || text.Length == 0)
            return;

        var attachments = PendingAttachments.ToArray();
        PendingAttachments.Clear();
        StatusText = null;
        State.Draft = string.Empty;
        Conversation.Add(new AssistantMessage(AssistantMessageRole.User, text) { Attachments = attachments });

        var answer = new AssistantMessage(AssistantMessageRole.Assistant) { IsStreaming = true };
        Conversation.Add(answer);

        using var source = new CancellationTokenSource();
        AnswerSource = source;
        IsStreaming = true;
        IsScrollPending = true;
        IsScrollForced = true;
        IsComposerResizePending = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var context = await ContextService.CaptureAsync(await ReadDocumentTitleAsync(), source.Token);
            var request = new AssistantMessageRequest
            {
                ConversationId = Conversation.Id,
                Text = text,
                Attachments = attachments,
                Context = context
            };

            await ReadAnswerAsync(request, answer, source.Token);
        }
        catch (OperationCanceledException)
        {
            answer.IsStopped = true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            answer.Error = new AssistantErrorEvent("client", Localizer["Unavailable"].Value, Retryable: true);
        }
        finally
        {
            answer.IsStreaming = false;
            answer.ToolStatus = null;

            if (ReferenceEquals(AnswerSource, source))
            {
                AnswerSource = null;
                IsStreaming = false;
            }

            IsScrollPending = true;
            if (!IsDisposed)
                await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ReadAnswerAsync(AssistantMessageRequest request, AssistantMessage answer, CancellationToken cancellationToken)
    {
        var lastRender = 0L;

        await foreach (var streamEvent in Client.SendAsync(request, cancellationToken))
        {
            Apply(answer, streamEvent);
            IsScrollPending = true;

            if (streamEvent is AssistantTextDeltaEvent && Stopwatch.GetElapsedTime(lastRender).TotalMilliseconds < RenderIntervalMilliseconds)
            {
                ScheduleRender();
                continue;
            }

            lastRender = Stopwatch.GetTimestamp();
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ScheduleRender()
    {
        if (IsRenderScheduled)
            return;

        IsRenderScheduled = true;
        _ = RenderLaterAsync();
    }

    private async Task RenderLaterAsync()
    {
        await Task.Delay((int)RenderIntervalMilliseconds);
        IsRenderScheduled = false;

        if (IsDisposed)
            return;

        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void Apply(AssistantMessage answer, AssistantStreamEvent streamEvent)
    {
        switch (streamEvent)
        {
            case AssistantConversationStartedEvent started:
                Conversation.Id = started.ConversationId;
                break;
            case AssistantTextDeltaEvent delta:
                answer.Append(delta.Text);
                break;
            case AssistantToolStatusEvent { State: AssistantToolState.Started } tool:
                answer.ToolId = tool.ToolId;
                answer.ToolStatus = tool.Label;
                break;
            case AssistantToolStatusEvent tool when answer.ToolId == tool.ToolId:
                answer.ToolId = null;
                answer.ToolStatus = null;
                break;
            case AssistantCitationEvent citation:
                AddCitation(answer, citation);
                break;
            case AssistantPendingActionEvent pending:
                answer.PendingAction = new AssistantPendingAction(pending);
                break;
            case AssistantFinishedEvent finished:
                answer.ServerId = finished.MessageId;
                answer.IsStreaming = false;
                break;
            case AssistantErrorEvent error:
                answer.Error = error;
                answer.IsStreaming = false;
                break;
        }
    }

    private static void AddCitation(AssistantMessage answer, AssistantCitationEvent citation)
    {
        if (answer.Citations.Any(existing => existing.Title == citation.Title && existing.Url == citation.Url))
            return;

        answer.Citations.Add(new AssistantCitation(citation.Title, citation.Url, citation.Snippet));
    }

    private Task StopAsync()
    {
        AnswerSource?.Cancel();
        return Task.CompletedTask;
    }

    private async Task NewConversationAsync()
    {
        AnswerSource?.Cancel();
        AnswerSource = null;
        IsStreaming = false;
        PendingAttachments.Clear();
        StatusText = null;
        State.Draft = string.Empty;
        Conversation.Reset();
        IsComposerResizePending = true;
        await ComposerElement.FocusAsync();
    }

    private async Task RateAsync(AssistantMessage message, AssistantRating rating)
    {
        if (message.Rating == rating)
            return;

        var previous = message.Rating;
        message.Rating = rating;

        try
        {
            await Client.SendFeedbackAsync(new AssistantFeedback
            {
                ConversationId = Conversation.Id,
                MessageId = message.FeedbackId,
                Rating = rating
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            message.Rating = previous;
            StatusText = Localizer["FeedbackFailed"].Value;
        }
    }

    private async Task ResolveActionAsync(AssistantMessage message, bool confirm)
    {
        if (message.PendingAction is not { State: AssistantActionState.Pending } action)
            return;

        action.State = AssistantActionState.Working;
        await InvokeAsync(StateHasChanged);

        try
        {
            var result = confirm
                ? await Client.ConfirmActionAsync(action.Id, Conversation.Id)
                : await Client.DeclineActionAsync(action.Id, Conversation.Id);

            action.ResultMessage = result.Message;
            action.State = !result.Succeeded ? AssistantActionState.Failed : confirm ? AssistantActionState.Confirmed : AssistantActionState.Declined;
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            action.ResultMessage = Localizer["Unavailable"].Value;
            action.State = AssistantActionState.Failed;
        }
    }

    private async Task OnFilesSelectedAsync(InputFileChangeEventArgs eventArgs)
    {
        StatusText = null;

        foreach (var file in eventArgs.GetMultipleFiles(MaxFilesPerSelection))
        {
            if (file.Size > Settings.MaxUploadBytes)
            {
                StatusText = string.Format(Localizer["UploadTooLarge"].Value, file.Name, Settings.MaxUploadBytes / (1024 * 1024));
                continue;
            }

            try
            {
                await using var stream = file.OpenReadStream(Settings.MaxUploadBytes);
                PendingAttachments.Add(await Client.UploadAsync(file.Name, file.ContentType, stream, Conversation.Id));
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException)
            {
                StatusText = string.Format(Localizer["UploadFailed"].Value, file.Name);
            }
        }
    }

    private async Task<string?> ReadDocumentTitleAsync()
    {
        if (Module is null)
            return null;

        try
        {
            return await Module.InvokeAsync<string>("documentTitle");
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException or TaskCanceledException)
        {
            return null;
        }
    }

    private async Task DetachAsync()
    {
        if (!IsAttached || Module is null)
            return;

        IsAttached = false;

        try
        {
            await Module.InvokeVoidAsync("detach");
        }
        catch (Exception exception) when (exception is JSDisconnectedException or ObjectDisposedException or TaskCanceledException)
        {
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        State.Changed -= OnStateChanged;
        AnswerSource?.Cancel();
        await DetachAsync();
        SelfReference?.Dispose();

        if (Module is null)
            return;

        try
        {
            await Module.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or ObjectDisposedException or TaskCanceledException)
        {
        }
    }
}
