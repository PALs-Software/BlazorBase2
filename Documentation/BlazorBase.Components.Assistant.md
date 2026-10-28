# BlazorBase.Components.Assistant

A generic assistant (chat) panel for the application shell, in the `BlazorBase.Components` package
(namespace `BlazorBase.Components.Assistant`): a panel with transcript, streaming answer, sources, feedback,
stop and new conversation, the stream event model, the page context that goes along with each message, and the
client that talks to a backend over Server-Sent Events. It knows nothing about a specific backend or product;
the backend only has to speak the event model below.

---

## Overview

| Concern | Type |
|---|---|
| Panel | `BaseAssistantPanel` |
| Open/close state and the conversation | `AssistantPanelState` (scoped) |
| Backend connection | `IAssistantChatClient`, `SseAssistantChatClient`, `ScriptedAssistantChatClient` |
| Settings | `AssistantClientOptions` |
| Stream events | `AssistantStreamEvent` and its records, `AssistantJson` |
| Request and feedback DTOs | `AssistantMessageRequest`, `AssistantAttachment`, `AssistantFeedback`, `AssistantActionResult` |
| Page context | `AssistantContextService` (scoped), `AssistantPageContext`, `IAssistantContextProvider`, `AssistantContextHint` |
| Registration | `AddBlazorBaseAssistant` |

---

## Registration and placement

```csharp
builder.Services.AddBlazorBaseAssistant(options =>
{
    options.SiteId = "dev.acadon";
    options.Endpoint = "/api/agenthub/chat";
    options.DisplayName = "acadon Hub";
    options.Suggestions = ["What can I ask?", "Explain this page"];
    options.PrepareRequestAsync = async (request, token) => request.Headers.Add("X-CSRF-TOKEN", await GetTokenAsync());
});
```

Place the panel once inside the layout, and toggle it from a header action:

```razor
<BaseAssistantPanel />
```

```razor
@inject AssistantPanelState Assistant
<FluentButton OnClick="Assistant.Toggle" aria-controls="base-assistant" aria-expanded="@Assistant.IsOpen" />
```

The default client needs an `HttpClient` in the container (WebAssembly hosts have one). A client registered before
`AddBlazorBaseAssistant` stays, so `services.AddScoped<IAssistantChatClient, ScriptedAssistantChatClient>()` runs the
UI without a backend. The overload `AddBlazorBaseAssistant(AssistantClientOptions)` takes an options instance.

### AssistantClientOptions

| Property | Default | Meaning |
|---|---|---|
| `SiteId` | null | Sent as `AssistantPageContext.SiteId`. |
| `Endpoint` | `/api/agenthub/chat` | The POST target; the answer is a `text/event-stream`. |
| `FeedbackEndpoint`, `UploadEndpoint`, `ActionsEndpoint` | null | Null uses `feedback`, `files` and `actions` next to `Endpoint`. |
| `DisplayName`, `AvatarUrl` | null | The persona; null shows the localized name and a generic icon. |
| `WelcomeText` | null | Text of the empty panel; null uses the localized default. |
| `Suggestions` | empty | Buttons shown while the conversation is empty. |
| `UploadEnabled` | false | Shows the attach button. |
| `MaxUploadBytes` | 10 MB | Largest accepted file. |
| `MaxContextBytes` | 4096 | Cap of the serialized page context. |
| `PrepareRequestAsync` | null | Called before every request, for example to add an anti-forgery header. |

`BaseAssistantPanel` has the parameters `DisplayName`, `AvatarUrl`, `WelcomeText` (override the options),
`AvatarContent` (a render fragment instead of the avatar) and `PanelId` (default `base-assistant`).

---

## The panel

- **Docked** at the end edge from 1200 px, next to the page; the width is resizable (drag the edge or use the arrow
  keys on the separator) and remembered in `localStorage`. When the layout has a `#base-main` element, the panel
  reserves its width next to it; otherwise it overlays the page.
- **Floating** over the page between 769 and 1199 px.
- **Bottom sheet** over a dimmed page up to 768 px; it follows the on-screen keyboard.
- Transcript with Markdown, code blocks with a copy button, sources, a tool status line, streaming text, a stop
  button, new conversation, suggestion chips (empty conversation), thumbs up/down on finished answers.
- Optional file upload (`UploadEnabled`): the file is uploaded through `IAssistantChatClient.UploadAsync` and sent as
  an `AssistantAttachment` with the next message.
- **Pending action card**: only shown when the stream sends a `pending_action` event. Confirm and decline call the
  client; the card then shows the result.
- Keyboard: Enter sends, Shift+Enter adds a line, Esc closes. The transcript is a `role="log"` region that is
  `aria-busy` while an answer streams. Light and dark theme follow the BlazorBase tokens.
- The conversation and the unsent draft live in `AssistantPanelState`, so they survive closing the panel and page
  navigation; they are not persisted across a page reload.

### Markdown and security

Answers are rendered by `MarkdownConverter`: raw HTML is escaped, only `http`, `https`, `mailto` and relative links
survive (scheme-relative `//host` links become text), images become links, so nothing is loaded automatically.
External links open in a new tab with `rel="noopener noreferrer"`. Code blocks are rendered as encoded text. A
registered `IHtmlSanitizer` is applied on top. Citation URLs are linked only when they are `http`, `https` or a
path starting with a single `/`. User messages are always plain text.

---

## Stream events

The endpoint answers with a `text/event-stream`. Each message carries one JSON object with a `type` discriminator;
the SSE `event:` line may carry the same name and is used when the JSON has no `type`. Unknown types are ignored.
`AssistantJson.Options` holds the JSON settings (camelCase, enums as camelCase strings, null properties omitted);
`AssistantJson.Serialize` and `EventName` produce a message on the server side.

| `type` | Record | Properties |
|---|---|---|
| `conversation_started` | `AssistantConversationStartedEvent` | `conversationId` |
| `text_delta` | `AssistantTextDeltaEvent` | `text` (Markdown fragment) |
| `tool_status` | `AssistantToolStatusEvent` | `toolId`, `label`, `state` (`started`, `completed`, `failed`) |
| `citation` | `AssistantCitationEvent` | `title`, `url`, `snippet` |
| `pending_action` | `AssistantPendingActionEvent` | `id`, `summary`, `parameters` (name to value) |
| `finished` | `AssistantFinishedEvent` | `messageId`, `inputTokens`, `outputTokens` |
| `error` | `AssistantErrorEvent` | `code`, `message`, `retryable` |

```text
event: conversation_started
data: {"type":"conversation_started","conversationId":"c-1"}

event: text_delta
data: {"type":"text_delta","text":"Open **Budgets**"}
```

`SseAssistantChatClient` turns transport problems into `error` events: `http_<status>` for a failed request
(`retryable` for 429 and 5xx), `network` for a broken connection, `invalid_event` for a message that is not valid
JSON (the stream goes on). Cancelling the token (the stop button) ends the stream with an
`OperationCanceledException`. Reconnecting is not attempted. The SSE reader (`ServerSentEventReader`) follows the
format: LF, CR and CRLF line ends, comments, multi-line `data`, and a message the stream ends in the middle of is
dropped.

### Requests

| Call | Method and URL | Body |
|---|---|---|
| Message | `POST {Endpoint}` | `AssistantMessageRequest`: `conversationId`, `text`, `attachments`, `context` |
| Feedback | `POST {FeedbackEndpoint}` | `AssistantFeedback`: `conversationId`, `messageId`, `rating` (`up`, `down`) |
| Upload | `POST {UploadEndpoint}` | multipart: `file`, `conversationId`; returns an `AssistantAttachment` |
| Confirm / decline | `POST {ActionsEndpoint}/{id}/confirm` or `/decline` | `{ "conversationId": ... }`; returns an `AssistantActionResult` |

`IAssistantChatClient` is the seam; implement it to talk to anything else.
`ScriptedAssistantChatClient` is a deterministic implementation for tests and UI development: `Reply(...)` queues
the events of the next answer, `ReplyWithText` queues a plain answer, without a queued reply it echoes the question
with a tool status and a citation; it records requests, feedback, uploads and decisions.

---

## Page context

`AssistantContextService.CaptureAsync()` builds the `AssistantPageContext` for a message: `SiteId`, `Route` (path
without query string), `PageTitle` (the service's `PageTitle`, else the document title), `Locale`, `Roles` (names
only), `EntityType`, `EntityKey`, `Filters`, `Preset`, `ErrorText` and free `Hints`. It holds ids and technical
hints only, never field values, and it is untrusted input for whoever receives it.

Contributions:

- `service.PageTitle`, `service.VisibleError`, `service.SetHint(key, value)` for state the application owns.
- `IAssistantContextProvider`: register with `service.Register(provider)` (dispose the handle when the part
  goes away); `Contribute` runs for every message and sees the state of that moment. A provider that throws is
  skipped. For single-valued properties the provider registered last wins.
- `<AssistantContextHint Key="wizard.step" Value="2" EntityType=... EntityKey=... Filters=... Preset=... ErrorText=... />`
  contributes while it is rendered and does nothing when the assistant is not registered.
- `BaseList` reports its model name, the key of the open item and the number of active filter conditions;
  `BaseCard` reports its model name and, for an existing record, the primary key. Both only when the assistant is
  registered.

The serialized context never exceeds `MaxContextBytes`: texts are shortened (`…`), at most 20 hints and 20 roles
are kept, then hints are dropped from the end, then the remaining values; `IsTruncated` tells the receiver.

---

## Localization and styling

English texts are the default; `BaseAssistantPanel.razor.de.resx` holds the German ones, the same resource serves
the message and code block components. Styles are scoped CSS using the shared tokens (`--color-*`, `--radius-*`);
the panel sets `--assistant-width` and `--assistant-keyboard-inset` itself.
