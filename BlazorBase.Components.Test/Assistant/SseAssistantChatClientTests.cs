using System.Net;
using System.Text;
using System.Text.Json;
using BlazorBase.Components.Assistant;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Components.Test.Assistant;

public sealed class SseAssistantChatClientTests
{
    private readonly RecordingHandler Handler = new();
    private readonly AssistantClientOptions Settings = new() { Endpoint = "/api/hub/chat" };

    private SseAssistantChatClient CreateClient() =>
        new(new HttpClient(Handler) { BaseAddress = new Uri("https://site.test") }, Options.Create(Settings));

    private static AssistantMessageRequest Request(string text = "Hello") => new() { Text = text };

    private static async Task<List<AssistantStreamEvent>> CollectAsync(IAsyncEnumerable<AssistantStreamEvent> events)
    {
        var list = new List<AssistantStreamEvent>();

        await foreach (var streamEvent in events)
            list.Add(streamEvent);

        return list;
    }

    [Fact]
    public async Task TheAnswerStream_IsParsedIntoEvents()
    {
        Handler.RespondWith(Sse(
            "event: conversation_started\ndata: {\"conversationId\":\"c1\"}\n\n" +
            "event: text_delta\ndata: {\"text\":\"Hel\"}\n\n" +
            "data: {\"type\":\"text_delta\",\"text\":\"lo\"}\n\n" +
            "event: finished\ndata: {\"messageId\":\"m1\"}\n\n"));

        var events = await CollectAsync(CreateClient().SendAsync(Request()));

        Assert.Equal(
            [new AssistantConversationStartedEvent("c1"), new AssistantTextDeltaEvent("Hel"), new AssistantTextDeltaEvent("lo"), new AssistantFinishedEvent("m1")],
            events);
    }

    [Fact]
    public async Task TheRequest_IsAJsonPostWithContextAndAnEventStreamAcceptHeader()
    {
        Handler.RespondWith(Sse(string.Empty));
        var request = new AssistantMessageRequest
        {
            Text = "Why?",
            ConversationId = "c1",
            Context = new AssistantPageContext { Route = "/budgets", Locale = "en-US" }
        };

        await CollectAsync(CreateClient().SendAsync(request));

        var sent = Assert.Single(Handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://site.test/api/hub/chat", sent.Uri);
        Assert.Contains("text/event-stream", sent.Accept);
        using var body = JsonDocument.Parse(sent.Body);
        Assert.Equal("Why?", body.RootElement.GetProperty("text").GetString());
        Assert.Equal("c1", body.RootElement.GetProperty("conversationId").GetString());
        Assert.Equal("/budgets", body.RootElement.GetProperty("context").GetProperty("route").GetString());
    }

    [Fact]
    public async Task APrepareHook_CanAddHeadersToEveryRequest()
    {
        Settings.PrepareRequestAsync = (message, _) =>
        {
            message.Headers.Add("X-CSRF-TOKEN", "token-1");
            return ValueTask.CompletedTask;
        };
        Handler.RespondWith(Sse(string.Empty));

        await CollectAsync(CreateClient().SendAsync(Request()));

        Assert.Equal("token-1", Assert.Single(Handler.Requests).Headers["X-CSRF-TOKEN"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "http_429", true)]
    [InlineData(HttpStatusCode.InternalServerError, "http_500", true)]
    [InlineData(HttpStatusCode.Forbidden, "http_403", false)]
    public async Task AFailedRequest_BecomesAnErrorEvent(HttpStatusCode status, string code, bool retryable)
    {
        Handler.RespondWith(new HttpResponseMessage(status));

        var events = await CollectAsync(CreateClient().SendAsync(Request()));

        var error = Assert.IsType<AssistantErrorEvent>(Assert.Single(events));
        Assert.Equal(code, error.Code);
        Assert.Equal(retryable, error.Retryable);
    }

    [Fact]
    public async Task ANetworkFailure_BecomesAnErrorEvent()
    {
        Handler.Throw(new HttpRequestException("offline"));

        var events = await CollectAsync(CreateClient().SendAsync(Request()));

        var error = Assert.IsType<AssistantErrorEvent>(Assert.Single(events));
        Assert.Equal("network", error.Code);
        Assert.True(error.Retryable);
    }

    [Fact]
    public async Task AMalformedMessage_BecomesAnErrorEventAndTheStreamGoesOn()
    {
        Handler.RespondWith(Sse(
            "event: text_delta\ndata: {broken\n\n" +
            "event: future\ndata: {\"x\":1}\n\n" +
            "data:\n\n" +
            "event: text_delta\ndata: {\"text\":\"ok\"}\n\n"));

        var events = await CollectAsync(CreateClient().SendAsync(Request()));

        Assert.Equal(2, events.Count);
        Assert.Equal("invalid_event", Assert.IsType<AssistantErrorEvent>(events[0]).Code);
        Assert.Equal(new AssistantTextDeltaEvent("ok"), events[1]);
    }

    [Fact]
    public async Task CancellingTheToken_EndsTheStreamWithACancellation()
    {
        Handler.RespondWith(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new NeverEndingStream()) { Headers = { { "Content-Type", "text/event-stream" } } } });
        using var source = new CancellationTokenSource();

        var read = Task.Run(() => CollectAsync(CreateClient().SendAsync(Request(), source.Token)));
        await Task.Delay(50);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
    }

    [Fact]
    public async Task Feedback_IsPostedNextToTheChatEndpoint()
    {
        Handler.RespondWith(new HttpResponseMessage(HttpStatusCode.NoContent));

        await CreateClient().SendFeedbackAsync(new AssistantFeedback { MessageId = "m1", Rating = AssistantRating.Down, ConversationId = "c1" });

        var sent = Assert.Single(Handler.Requests);
        Assert.Equal("https://site.test/api/hub/feedback", sent.Uri);
        using var body = JsonDocument.Parse(sent.Body);
        Assert.Equal("down", body.RootElement.GetProperty("rating").GetString());
        Assert.Equal("m1", body.RootElement.GetProperty("messageId").GetString());
    }

    [Theory]
    [InlineData(true, "confirm")]
    [InlineData(false, "decline")]
    public async Task ConfirmAndDecline_PostToTheActionAndReturnTheResult(bool confirm, string verb)
    {
        Handler.RespondWith(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"succeeded\":true,\"message\":\"Done\"}", Encoding.UTF8, "application/json")
        });
        var client = CreateClient();

        var result = confirm
            ? await client.ConfirmActionAsync("a 1", "c1")
            : await client.DeclineActionAsync("a 1", "c1");

        Assert.Equal(new AssistantActionResult(true, "Done"), result);
        Assert.Equal($"https://site.test/api/hub/actions/a%201/{verb}", Assert.Single(Handler.Requests).Uri);
    }

    [Fact]
    public async Task AFailedConfirmation_ReturnsAFailedResultInsteadOfThrowing()
    {
        Handler.RespondWith(new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await CreateClient().ConfirmActionAsync("a1", null);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AnUpload_IsPostedAsMultipartAndReturnsTheAttachment()
    {
        Handler.RespondWith(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"f1\",\"fileName\":\"a.txt\",\"contentType\":\"text/plain\",\"size\":3}", Encoding.UTF8, "application/json")
        });
        using var content = new MemoryStream("abc"u8.ToArray());

        var attachment = await CreateClient().UploadAsync("a.txt", "text/plain", content, "c1");

        Assert.Equal(new AssistantAttachment("f1", "a.txt", "text/plain", 3), attachment);
        var sent = Assert.Single(Handler.Requests);
        Assert.Equal("https://site.test/api/hub/files", sent.Uri);
        Assert.Contains("filename=a.txt", sent.Body);
        Assert.Contains("abc", sent.Body);
    }

    [Fact]
    public void TheSiblingEndpoints_FollowTheChatEndpointUnlessConfigured()
    {
        var options = new AssistantClientOptions { Endpoint = "/api/hub/chat" };

        Assert.Equal("/api/hub/feedback", options.ResolveFeedbackEndpoint());
        Assert.Equal("/api/hub/files", options.ResolveUploadEndpoint());
        Assert.Equal("/api/hub/actions", options.ResolveActionsEndpoint());

        options.FeedbackEndpoint = "/custom/fb";
        Assert.Equal("/custom/fb", options.ResolveFeedbackEndpoint());
    }

    private static HttpResponseMessage Sse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };

    private sealed record SentRequest(HttpMethod Method, string Uri, string Accept, string Body, Dictionary<string, string> Headers);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private Func<HttpResponseMessage> Response = () => new HttpResponseMessage(HttpStatusCode.OK);

        public List<SentRequest> Requests { get; } = [];

        public void RespondWith(HttpResponseMessage response) => Response = () => response;

        public void Throw(Exception exception) => Response = () => throw exception;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new SentRequest(
                request.Method,
                request.RequestUri!.OriginalString,
                request.Headers.Accept.ToString(),
                body,
                request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value))));
            return Response();
        }
    }

    private sealed class NeverEndingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
