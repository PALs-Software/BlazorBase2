using System.Collections;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using BlazorBase.Components.Assistant;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Components.Test.Assistant;

public sealed class BaseAssistantPanelTests : ComponentsBunitTestContextBase
{
    private readonly ScriptedAssistantChatClient Client = new();
    private readonly AssistantClientOptions Settings = new() { SiteId = "test.site" };
    private readonly EnglishCulture English = new();

    public BaseAssistantPanelTests()
    {
        Services.AddLocalization();
        Services.AddSingleton<IAssistantChatClient>(Client);
        Services.AddBlazorBaseAssistant(Settings);
    }

    private AssistantPanelState State => Services.GetRequiredService<AssistantPanelState>();

    private IRenderedComponent<BaseAssistantPanel> RenderOpenPanel(Action<ComponentParameterCollectionBuilder<BaseAssistantPanel>>? parameters = null)
    {
        State.Open();
        return Render<BaseAssistantPanel>(parameters);
    }

    private static void Send(IRenderedComponent<BaseAssistantPanel> cut, string text)
    {
        cut.Find("textarea").Input(text);
        cut.Find(".assistant-send").Click();
    }

    protected override void Dispose(bool disposing)
    {
        English.Dispose();
        base.Dispose(disposing);
    }

    [Fact]
    public void AClosedPanel_RendersNothingAndOpensThroughTheState()
    {
        var cut = Render<BaseAssistantPanel>();

        Assert.Empty(cut.FindAll(".assistant-panel"));

        State.Open();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-panel")));

        State.Toggle();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".assistant-panel")));
    }

    [Fact]
    public void TheOpenPanel_ShowsTheDefaultPersonaWelcomeTextAndSuggestions()
    {
        Settings.Suggestions = ["What can I ask?", "Explain this page"];

        var cut = RenderOpenPanel();

        Assert.Equal("Assistant", cut.Find(".assistant-title").TextContent);
        Assert.Contains("Ask a question about this page.", cut.Find(".assistant-welcome").TextContent);
        Assert.Equal(["What can I ask?", "Explain this page"], cut.FindAll(".assistant-chip").Select(chip => chip.TextContent.Trim()));
        Assert.Empty(cut.FindAll(".assistant-avatar img"));
        Assert.Equal("complementary", cut.Find(".assistant-panel").GetAttribute("role"));
        Assert.Equal("base-assistant-title", cut.Find(".assistant-panel").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void ThePersona_CanComeFromTheOptionsOrBeOverriddenByParameters()
    {
        Settings.DisplayName = "acadon Hub";
        Settings.AvatarUrl = "/img/hub.png";
        Settings.WelcomeText = "Welcome from the options.";

        var cut = RenderOpenPanel();

        Assert.Equal("acadon Hub", cut.Find(".assistant-title").TextContent);
        Assert.Equal("/img/hub.png", cut.Find(".assistant-avatar img").GetAttribute("src"));
        Assert.Contains("Welcome from the options.", cut.Find(".assistant-welcome").TextContent);

        cut.Render(parameters => parameters.Add(p => p.DisplayName, "Helper").Add(p => p.AvatarUrl, "/img/other.png"));

        Assert.Equal("Helper", cut.Find(".assistant-title").TextContent);
        Assert.Equal("/img/other.png", cut.Find(".assistant-avatar img").GetAttribute("src"));
    }

    [Fact]
    public void EscapeAndTheCloseButton_CloseThePanel()
    {
        var cut = RenderOpenPanel();

        cut.Find(".assistant-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.False(State.IsOpen);

        State.Open();
        cut.WaitForAssertion(() => cut.Find(".assistant-close").Click());
        Assert.False(State.IsOpen);
    }

    [Fact]
    public void SendingAMessage_ShowsItAndTheStreamedAnswerWithSourcesAndFeedback()
    {
        Client.Reply(
            new AssistantConversationStartedEvent("c-1"),
            new AssistantTextDeltaEvent("Open **Budgets** "),
            new AssistantTextDeltaEvent("and edit the limit."),
            new AssistantCitationEvent("Budgets and boosts", "https://docs.example.com/budgets", "How budgets work"),
            new AssistantCitationEvent("Budgets and boosts", "https://docs.example.com/budgets"),
            new AssistantCitationEvent("Local page", "/budgets"),
            new AssistantCitationEvent("Unsafe", "javascript:alert(1)"),
            new AssistantFinishedEvent("m-1"));
        var cut = RenderOpenPanel();

        Send(cut, "How do I raise the budget?");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".assistant-feedback")));
        Assert.Contains("How do I raise the budget?", cut.Find(".assistant-message.user").TextContent);
        Assert.Contains("<strong>Budgets</strong>", cut.Find(".assistant-markdown").InnerHtml);
        var links = cut.FindAll(".assistant-citations li");
        Assert.Equal(3, links.Count);
        var external = links[0].QuerySelector("a")!;
        Assert.Equal("https://docs.example.com/budgets", external.GetAttribute("href"));
        Assert.Equal("_blank", external.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", external.GetAttribute("rel"));
        Assert.Null(links[1].QuerySelector("a")!.GetAttribute("target"));
        Assert.Null(links[2].QuerySelector("a"));
        Assert.Equal("Unsafe", links[2].TextContent.Trim());
        Assert.Empty(cut.FindAll(".assistant-stop"));
        Assert.Equal("", cut.Find("textarea").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void TheRequest_CarriesTheConversationIdAndThePageContext()
    {
        Client.Reply(new AssistantConversationStartedEvent("c-1"), new AssistantFinishedEvent());
        Services.GetRequiredService<AssistantContextService>().SetHint("wizard.step", "2");
        var cut = RenderOpenPanel();

        Send(cut, "First");
        cut.WaitForAssertion(() => Assert.Single(Client.Requests));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".assistant-stop")));
        Send(cut, "Second");

        cut.WaitForAssertion(() => Assert.Equal(2, Client.Requests.Count));
        Assert.Null(Client.Requests[0].ConversationId);
        Assert.Equal("c-1", Client.Requests[1].ConversationId);
        Assert.Equal("test.site", Client.Requests[0].Context!.SiteId);
        Assert.Equal("2", Client.Requests[0].Context!.Hints["wizard.step"]);
    }

    [Fact]
    public void ASuggestion_SendsItAsAMessage()
    {
        Settings.Suggestions = ["Explain this page"];
        var cut = RenderOpenPanel();

        cut.Find(".assistant-chip").Click();

        cut.WaitForAssertion(() => Assert.Equal("Explain this page", Assert.Single(Client.Requests).Text));
        Assert.Empty(cut.FindAll(".assistant-welcome"));
    }

    [Fact]
    public void ATextWithOnlyWhitespace_CannotBeSent()
    {
        var cut = RenderOpenPanel();

        cut.Find("textarea").Input("   ");

        Assert.NotNull(cut.Find(".assistant-send").GetAttribute("disabled"));
        Assert.Empty(Client.Requests);
    }

    [Fact]
    public void WhileTheAnswerStreams_TheToolStatusAndTheStopButtonAreShown()
    {
        var gated = UseGatedClient();
        var cut = RenderOpenPanel();

        Send(cut, "Search please");
        gated.Emit(new AssistantToolStatusEvent("search", "Searching documentation…"));

        cut.WaitForAssertion(() => Assert.Equal("Searching documentation…", cut.Find(".assistant-tool").TextContent.Trim()));
        Assert.Single(cut.FindAll(".assistant-stop"));
        Assert.Empty(cut.FindAll(".assistant-send"));
        Assert.Equal("true", cut.Find(".assistant-message.assistant").GetAttribute("aria-busy"));

        gated.Emit(new AssistantToolStatusEvent("search", "Searching documentation…", AssistantToolState.Completed));
        gated.Emit(new AssistantTextDeltaEvent("Found it."));
        gated.Emit(new AssistantFinishedEvent());
        gated.Complete();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".assistant-stop")));
        Assert.Empty(cut.FindAll(".assistant-tool"));
        Assert.Contains("Found it.", cut.Find(".assistant-markdown").TextContent);
        Assert.Null(cut.Find(".assistant-message.assistant").GetAttribute("aria-busy"));
    }

    [Fact]
    public void Stop_EndsTheAnswerKeepsThePartialTextAndMarksItStopped()
    {
        var gated = UseGatedClient();
        var cut = RenderOpenPanel();

        Send(cut, "Long answer");
        gated.Emit(new AssistantTextDeltaEvent("Partial text"));
        cut.WaitForAssertion(() => Assert.Contains("Partial text", cut.Markup));

        cut.Find(".assistant-stop").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-stopped")));
        Assert.Contains("Partial text", cut.Find(".assistant-markdown").TextContent);
        Assert.Single(cut.FindAll(".assistant-send"));
        Assert.True(gated.WasCancelled);
    }

    [Fact]
    public void NewConversation_StopsTheAnswerClearsTheTranscriptAndForgetsTheConversationId()
    {
        Client.Reply(new AssistantConversationStartedEvent("c-1"), new AssistantTextDeltaEvent("Hi"), new AssistantFinishedEvent());
        var cut = RenderOpenPanel();
        Assert.NotNull(cut.Find(".assistant-new").GetAttribute("disabled"));

        Send(cut, "Hello");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".assistant-feedback")));
        cut.Find(".assistant-new").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-welcome")));
        Assert.Empty(cut.FindAll(".assistant-message"));
        Assert.Null(State.Conversation.Id);

        Send(cut, "Again");
        cut.WaitForAssertion(() => Assert.Equal(2, Client.Requests.Count));
        Assert.Null(Client.Requests[1].ConversationId);
    }

    [Fact]
    public void NewConversationDuringAnAnswer_CancelsTheStream()
    {
        var gated = UseGatedClient();
        var cut = RenderOpenPanel();

        Send(cut, "Question");
        gated.Emit(new AssistantTextDeltaEvent("Some text"));
        cut.WaitForAssertion(() => Assert.Contains("Some text", cut.Markup));

        cut.Find(".assistant-new").Click();

        cut.WaitForAssertion(() => Assert.True(gated.WasCancelled));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-send")));
        Assert.Empty(cut.FindAll(".assistant-message"));
    }

    [Fact]
    public void AnErrorEvent_IsShownAsAnAlertWithoutFeedbackButtons()
    {
        Client.Reply(new AssistantErrorEvent("rate_limited", "Too many requests, try again later.", true));
        var cut = RenderOpenPanel();

        Send(cut, "Hello");

        cut.WaitForAssertion(() => Assert.Equal("Too many requests, try again later.", cut.Find(".assistant-error").TextContent));
        Assert.Equal("alert", cut.Find(".assistant-error").GetAttribute("role"));
        Assert.Empty(cut.FindAll(".assistant-feedback"));
        Assert.Single(cut.FindAll(".assistant-send"));
    }

    [Fact]
    public void AClientThatThrows_ShowsAGenericErrorInsteadOfCrashing()
    {
        Services.AddSingleton<IAssistantChatClient>(new ThrowingClient());
        var cut = RenderOpenPanel();

        Send(cut, "Hello");

        cut.WaitForAssertion(() => Assert.Contains("not available", cut.Find(".assistant-error").TextContent));
    }

    [Fact]
    public void Feedback_IsSentOnceWithTheServerMessageIdAndReflectedInThePressedState()
    {
        Client.Reply(new AssistantConversationStartedEvent("c-1"), new AssistantTextDeltaEvent("Answer"), new AssistantFinishedEvent("m-9"));
        var cut = RenderOpenPanel();
        Send(cut, "Hello");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".assistant-thumb-up")));

        cut.Find(".assistant-thumb-up").Click();
        cut.Find(".assistant-thumb-up").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".assistant-thumb-up").GetAttribute("aria-pressed")));
        var feedback = Assert.Single(Client.Feedbacks);
        Assert.Equal(new AssistantFeedback { ConversationId = "c-1", MessageId = "m-9", Rating = AssistantRating.Up }, feedback);

        cut.Find(".assistant-thumb-down").Click();
        cut.WaitForAssertion(() => Assert.Equal(2, Client.Feedbacks.Count));
        Assert.Equal("false", cut.Find(".assistant-thumb-up").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void AnAnswerWithoutAPendingAction_ShowsNoConfirmationCard()
    {
        Client.ReplyWithText("Just text");
        var cut = RenderOpenPanel();

        Send(cut, "Hello");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".assistant-feedback")));
        Assert.Empty(cut.FindAll(".assistant-action"));
    }

    [Fact]
    public void APendingAction_ShowsTheConfirmationCardAndConfirmingCallsTheClient()
    {
        Client.Reply(
            new AssistantTextDeltaEvent("I prepared this."),
            new AssistantPendingActionEvent("act-1", "Set the budget of team Platform to 500", new Dictionary<string, string> { ["Team"] = "Platform", ["Limit"] = "500" }),
            new AssistantFinishedEvent());
        Client.ConfirmResult = new AssistantActionResult(true, "Budget updated.");
        var cut = RenderOpenPanel();

        Send(cut, "Boost the team");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-action")));
        Assert.Equal("Set the budget of team Platform to 500", cut.Find(".assistant-action-summary").TextContent);
        Assert.Equal(["Team", "Limit"], cut.FindAll(".assistant-action-parameters dt").Select(term => term.TextContent));
        Assert.Contains("Needs your confirmation", cut.Find(".assistant-action-kind").TextContent);

        cut.Find(".assistant-action-confirm").Click();

        cut.WaitForAssertion(() => Assert.Contains("Budget updated.", cut.Find(".assistant-action-result").TextContent));
        Assert.Equal(["act-1"], Client.ConfirmedActions);
        Assert.Empty(Client.DeclinedActions);
        Assert.Empty(cut.FindAll(".assistant-action-confirm"));
        Assert.Contains("confirmed", cut.Find(".assistant-action").ClassList);
    }

    [Fact]
    public void DecliningAPendingAction_CallsTheClientAndEndsTheCard()
    {
        Client.Reply(new AssistantPendingActionEvent("act-2", "Delete the record"), new AssistantFinishedEvent());
        var cut = RenderOpenPanel();
        Send(cut, "Delete it");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-action-decline")));

        cut.Find(".assistant-action-decline").Click();

        cut.WaitForAssertion(() => Assert.Contains("declined", cut.Find(".assistant-action").ClassList));
        Assert.Equal(["act-2"], Client.DeclinedActions);
        Assert.Empty(Client.ConfirmedActions);
        Assert.Empty(cut.FindAll(".assistant-action-buttons"));
    }

    [Fact]
    public void AFailedConfirmation_IsShownAsNotDone()
    {
        Client.Reply(new AssistantPendingActionEvent("act-3", "Change something"), new AssistantFinishedEvent());
        Client.ConfirmResult = new AssistantActionResult(false, "You may not do this.");
        var cut = RenderOpenPanel();
        Send(cut, "Do it");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-action-confirm")));

        cut.Find(".assistant-action-confirm").Click();

        cut.WaitForAssertion(() => Assert.Contains("failed", cut.Find(".assistant-action").ClassList));
        Assert.Contains("You may not do this.", cut.Find(".assistant-action-result").TextContent);
    }

    [Fact]
    public void ModelOutputWithHtml_IsNeverRenderedAsHtml()
    {
        Client.ReplyWithText(
            "Try <script>alert(1)</script> <img src=x onerror=alert(2)> <iframe src=//evil></iframe> " +
            "[click](javascript:alert(3)) [mail](mailto:a@b.c) ![pixel](https://tracker.example/p.png) <a href=\"javascript:alert(4)\">raw</a>");
        var cut = RenderOpenPanel();

        Send(cut, "Hello");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".assistant-feedback")));
        var answer = cut.Find(".assistant-markdown");
        Assert.Empty(cut.FindAll("script"));
        Assert.Empty(cut.FindAll("iframe"));
        Assert.Empty(cut.FindAll(".assistant-markdown img"));
        Assert.Empty(cut.FindAll("[onerror]"));
        Assert.DoesNotContain(cut.FindAll("a"), link => (link.GetAttribute("href") ?? string.Empty).StartsWith("javascript", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("&lt;script&gt;", answer.InnerHtml);
        Assert.Contains("mailto:a@b.c", answer.InnerHtml);
    }

    [Fact]
    public void AUserMessageWithHtml_IsShownAsText()
    {
        var cut = RenderOpenPanel();

        Send(cut, "<b>bold</b><script>alert(1)</script>");

        cut.WaitForAssertion(() => Assert.Contains("<b>bold</b>", cut.Find(".assistant-message.user .assistant-bubble").TextContent));
        Assert.Empty(cut.FindAll(".assistant-message.user b"));
        Assert.Empty(cut.FindAll("script"));
    }

    [Fact]
    public void ACodeBlock_ShowsTheCodeAsTextAndCopiesItToTheClipboard()
    {
        Client.ReplyWithText("Run this:\n\n```bash\nnpm install <pkg>\n```\n\nDone.");
        var cut = RenderOpenPanel();

        Send(cut, "How?");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-code")));
        Assert.Equal("npm install <pkg>", cut.Find(".assistant-code code").TextContent);
        Assert.Equal("language-bash", cut.Find(".assistant-code code").GetAttribute("class"));
        Assert.Equal("bash", cut.Find(".assistant-code-language").TextContent);

        cut.Find(".assistant-code-copy").Click();

        var invocation = JSInterop.VerifyInvoke("navigator.clipboard.writeText");
        Assert.Equal("npm install <pkg>", invocation.Arguments[0]);
        cut.WaitForAssertion(() => Assert.Equal("Copied", cut.Find(".assistant-code-copy span").TextContent));
    }

    [Fact]
    public void UploadIsOffByDefault()
    {
        var cut = RenderOpenPanel();

        Assert.Empty(cut.FindAll(".assistant-attach"));
        Assert.Empty(cut.FindAll("input[type=file]"));
    }

    [Fact]
    public void WithUploadEnabled_AFileIsUploadedShownAndSentWithTheNextMessage()
    {
        Settings.UploadEnabled = true;
        var cut = RenderOpenPanel();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "notes.txt"));

        cut.WaitForAssertion(() => Assert.Contains("notes.txt", cut.Find(".assistant-pending-attachments").TextContent));
        Assert.Equal(["notes.txt"], Client.Uploads);

        Send(cut, "See the file");

        cut.WaitForAssertion(() => Assert.Single(Client.Requests));
        Assert.Equal("notes.txt", Assert.Single(Client.Requests[0].Attachments).FileName);
        Assert.Empty(cut.FindAll(".assistant-pending-attachments"));
        Assert.Contains("notes.txt", cut.Find(".assistant-attachments").TextContent);
    }

    [Fact]
    public void AFileThatIsTooLarge_IsRejectedWithAMessage()
    {
        Settings.UploadEnabled = true;
        Settings.MaxUploadBytes = 3;
        var cut = RenderOpenPanel();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("too long", "big.txt"));

        cut.WaitForAssertion(() => Assert.Contains("big.txt is larger than", cut.Find(".assistant-status").TextContent));
        Assert.Empty(Client.Uploads);
    }

    [Fact]
    public void AnAttachedFile_CanBeRemovedBeforeSending()
    {
        Settings.UploadEnabled = true;
        var cut = RenderOpenPanel();
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "notes.txt"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-remove-attachment")));

        cut.Find(".assistant-remove-attachment").Click();

        Assert.Empty(cut.FindAll(".assistant-pending-attachments"));
    }

    [Fact]
    public async Task TheKeyboardSubmit_SendsTheTextOfTheComposer()
    {
        var cut = RenderOpenPanel();

        await cut.InvokeAsync(() => cut.Instance.SubmitFromKeyboardAsync("Sent with Enter"));

        Assert.Equal("Sent with Enter", Assert.Single(Client.Requests).Text);
        cut.WaitForAssertion(() => Assert.Contains("Sent with Enter", cut.Find(".assistant-message.user").TextContent));
    }

    [Fact]
    public void TheConversation_SurvivesClosingAndReopeningThePanel()
    {
        Client.ReplyWithText("Remember me");
        var cut = RenderOpenPanel();
        Send(cut, "Hello");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".assistant-feedback")));

        State.Close();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".assistant-panel")));
        State.Open();

        cut.WaitForAssertion(() => Assert.Contains("Remember me", cut.Find(".assistant-markdown").TextContent));
    }

    [Fact]
    public void TheDraft_IsKeptWhileThePanelIsClosed()
    {
        var cut = RenderOpenPanel();
        cut.Find("textarea").Input("Half typed");

        State.Close();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".assistant-panel")));
        State.Open();

        cut.WaitForAssertion(() => Assert.Equal("Half typed", cut.Find("textarea").GetAttribute("value")));
    }

    [Fact]
    public void TheTranscript_IsALogRegionAndTheComposerIsLabelled()
    {
        var cut = RenderOpenPanel();

        Assert.Equal("log", cut.Find(".assistant-transcript").GetAttribute("role"));
        Assert.Equal("Conversation", cut.Find(".assistant-transcript").GetAttribute("aria-label"));
        Assert.Equal("Ask a question…", cut.Find("textarea").GetAttribute("aria-label"));
        Assert.Equal("Send message", cut.Find(".assistant-send").GetAttribute("aria-label"));
        Assert.Equal("separator", cut.Find(".assistant-resize").GetAttribute("role"));
    }

    [Fact]
    public void WithAGermanUiCulture_TheTextsAreGerman()
    {
        using var german = new CultureScope("de");

        var cut = RenderOpenPanel();

        Assert.Equal("Assistent", cut.Find(".assistant-title").TextContent);
        Assert.Equal("Nachricht senden", cut.Find(".assistant-send").GetAttribute("aria-label"));
    }

    [Fact]
    public void GermanResources_CoverEveryEnglishKey()
    {
        var english = ReadResources(CultureInfo.InvariantCulture);
        var german = ReadResources(new CultureInfo("de"));

        Assert.NotEmpty(english);
        Assert.Empty(english.Keys.Except(german.Keys));
        Assert.Empty(german.Keys.Except(english.Keys));
        Assert.All(german, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
    }

    private GatedClient UseGatedClient()
    {
        var gated = new GatedClient();
        Services.AddSingleton<IAssistantChatClient>(gated);
        return gated;
    }

    private static Dictionary<string, string> ReadResources(CultureInfo culture)
    {
        var assembly = typeof(BaseAssistantPanel).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith(".BaseAssistantPanel.resources", StringComparison.Ordinal));
        var manager = new ResourceManager(resourceName[..^".resources".Length], assembly);
        var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!;

        return set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo Previous = CultureInfo.CurrentUICulture;

        public CultureScope(string name) => CultureInfo.CurrentUICulture = new CultureInfo(name);

        public void Dispose() => CultureInfo.CurrentUICulture = Previous;
    }

    private sealed class ThrowingClient : ScriptedBase
    {
        public override async IAsyncEnumerable<AssistantStreamEvent> SendAsync(AssistantMessageRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new InvalidOperationException("backend down");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class GatedClient : ScriptedBase
    {
        private readonly System.Threading.Channels.Channel<AssistantStreamEvent?> Events = System.Threading.Channels.Channel.CreateUnbounded<AssistantStreamEvent?>();

        public bool WasCancelled { get; private set; }

        public void Emit(AssistantStreamEvent streamEvent) => Events.Writer.TryWrite(streamEvent);

        public void Complete() => Events.Writer.TryWrite(null);

        public override async IAsyncEnumerable<AssistantStreamEvent> SendAsync(AssistantMessageRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (true)
            {
                AssistantStreamEvent? next;

                try
                {
                    next = await Events.Reader.ReadAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    WasCancelled = true;
                    throw;
                }

                if (next is null)
                    yield break;

                yield return next;
            }
        }
    }

    private abstract class ScriptedBase : IAssistantChatClient
    {
        public abstract IAsyncEnumerable<AssistantStreamEvent> SendAsync(AssistantMessageRequest request, CancellationToken cancellationToken = default);

        public Task SendFeedbackAsync(AssistantFeedback feedback, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<AssistantAttachment> UploadAsync(string fileName, string contentType, Stream content, string? conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AssistantAttachment("x", fileName, contentType, 0));

        public Task<AssistantActionResult> ConfirmActionAsync(string actionId, string? conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AssistantActionResult(true));

        public Task<AssistantActionResult> DeclineActionAsync(string actionId, string? conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AssistantActionResult(true));
    }
}
