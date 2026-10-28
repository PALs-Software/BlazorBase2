using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Assistant;

/// <summary>
/// Talks to an assistant backend over HTTP: the message is posted as JSON and the answer comes back as a
/// <c>text/event-stream</c> whose messages carry the JSON of an <see cref="AssistantStreamEvent"/>.
/// </summary>
/// <remarks>
/// A failed request, an unreadable message or a stream that breaks becomes an <see cref="AssistantErrorEvent"/>
/// (<c>http_&lt;status&gt;</c>, <c>network</c>, <c>invalid_event</c>); cancelling the token ends the stream with an
/// <see cref="OperationCanceledException"/>. Reconnecting is not attempted: the user sends the message again.
/// </remarks>
public sealed class SseAssistantChatClient(HttpClient httpClient, IOptions<AssistantClientOptions> options) : IAssistantChatClient
{
    #region Injects

    private readonly HttpClient HttpClient = httpClient;
    private readonly IOptions<AssistantClientOptions> Options = options;

    #endregion

    private AssistantClientOptions Settings => Options.Value;

    /// <inheritdoc/>
    public async IAsyncEnumerable<AssistantStreamEvent> SendAsync(AssistantMessageRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        HttpResponseMessage? response = null;
        AssistantErrorEvent? failure = null;

        try
        {
            using var message = await CreateRequestAsync(HttpMethod.Post, Settings.Endpoint, JsonContent.Create(request, options: AssistantJson.Options), cancellationToken);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            response = await HttpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                failure = new AssistantErrorEvent($"http_{(int)response.StatusCode}", response.ReasonPhrase ?? response.StatusCode.ToString(), IsRetryable(response.StatusCode));
        }
        catch (HttpRequestException exception)
        {
            failure = new AssistantErrorEvent("network", exception.Message, Retryable: true);
        }

        if (failure is not null)
        {
            response?.Dispose();
            yield return failure;
            yield break;
        }

        using (response)
        {
            var stream = await response!.Content.ReadAsStreamAsync(cancellationToken);
            var messages = ServerSentEventReader.ReadAsync(stream, cancellationToken).GetAsyncEnumerator(cancellationToken);

            try
            {
                while (true)
                {
                    ServerSentEvent? current = null;
                    AssistantErrorEvent? streamFailure = null;

                    try
                    {
                        current = await messages.MoveNextAsync() ? messages.Current : null;
                    }
                    catch (Exception exception) when (exception is IOException or HttpRequestException)
                    {
                        streamFailure = new AssistantErrorEvent("network", exception.Message, Retryable: true);
                    }

                    if (streamFailure is not null)
                    {
                        yield return streamFailure;
                        yield break;
                    }

                    if (current is null)
                        yield break;

                    if (string.IsNullOrWhiteSpace(current.Data))
                        continue;

                    var decoded = Decode(current);

                    if (decoded is not null)
                        yield return decoded;
                }
            }
            finally
            {
                await messages.DisposeAsync();
            }
        }
    }

    /// <inheritdoc/>
    public async Task SendFeedbackAsync(AssistantFeedback feedback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        using var message = await CreateRequestAsync(HttpMethod.Post, Settings.ResolveFeedbackEndpoint(), JsonContent.Create(feedback, options: AssistantJson.Options), cancellationToken);
        using var response = await HttpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task<AssistantAttachment> UploadAsync(string fileName, string contentType, Stream content, string? conversationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        var form = new MultipartFormDataContent { { fileContent, "file", fileName } };

        if (conversationId is not null)
            form.Add(new StringContent(conversationId), "conversationId");

        using var message = await CreateRequestAsync(HttpMethod.Post, Settings.ResolveUploadEndpoint(), form, cancellationToken);
        using var response = await HttpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AssistantAttachment>(AssistantJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("The upload returned no attachment.");
    }

    /// <inheritdoc/>
    public Task<AssistantActionResult> ConfirmActionAsync(string actionId, string? conversationId, CancellationToken cancellationToken = default) =>
        ResolveActionAsync(actionId, "confirm", conversationId, cancellationToken);

    /// <inheritdoc/>
    public Task<AssistantActionResult> DeclineActionAsync(string actionId, string? conversationId, CancellationToken cancellationToken = default) =>
        ResolveActionAsync(actionId, "decline", conversationId, cancellationToken);

    private async Task<AssistantActionResult> ResolveActionAsync(string actionId, string decision, string? conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        var url = $"{Settings.ResolveActionsEndpoint().TrimEnd('/')}/{Uri.EscapeDataString(actionId)}/{decision}";
        using var message = await CreateRequestAsync(HttpMethod.Post, url, JsonContent.Create(new { conversationId }, options: AssistantJson.Options), cancellationToken);
        using var response = await HttpClient.SendAsync(message, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new AssistantActionResult(false, response.ReasonPhrase);

        return await response.Content.ReadFromJsonAsync<AssistantActionResult>(AssistantJson.Options, cancellationToken)
            ?? new AssistantActionResult(true);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url, HttpContent content, CancellationToken cancellationToken)
    {
        var message = new HttpRequestMessage(method, url) { Content = content };

        if (Settings.PrepareRequestAsync is { } prepare)
            await prepare(message, cancellationToken);

        return message;
    }

    private static AssistantStreamEvent? Decode(ServerSentEvent serverEvent)
    {
        try
        {
            return AssistantJson.Deserialize(serverEvent.Data, serverEvent.Event);
        }
        catch (JsonException)
        {
            return new AssistantErrorEvent("invalid_event", "The assistant sent a message that could not be read.");
        }
    }

    private static bool IsRetryable(HttpStatusCode status) => status is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError;
}
