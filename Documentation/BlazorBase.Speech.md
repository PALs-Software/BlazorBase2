# BlazorBase.Speech

Browser-safe Razor Class Library (net10.0) for spoken input and output: a push-to-talk button that
records 16 kHz mono WAV, a narrator that reads text aloud segment by segment, the Markdown-to-speech
text preparation behind it, and the HTTP client for the host's speech proxy. It is the client
companion to **BlazorBase.Speech.Server**, which proxies to an internal speech service.

Works under WebAssembly and Blazor Server alike: audio crosses the JS boundary as streams
(`IJSStreamReference` / `DotNetStreamReference`), never as one serialized argument, so Blazor Server's
SignalR message size limit does not apply.

---

## Overview

| Concern | Type |
|---|---|
| Push-to-talk button | `BlazorBase.Speech.Components.PushToTalk.PushToTalkButton` |
| Reading text aloud | `BlazorBase.Speech.Services.ISpeechNarrator` / `SpeechNarrator` |
| Playback | `BlazorBase.Speech.Services.ISpeechAudioPlayer` / `JsSpeechAudioPlayer` |
| Markdown → speakable segments | `BlazorBase.Speech.Text.SpeechTextPreparer` |
| German/English guess for the voice | `BlazorBase.Speech.Text.SpeechLanguageGuesser` |
| Proxy client | `BlazorBase.Speech.Services.ISpeechClient` / `HttpSpeechClient` |
| Localized error texts | `BlazorBase.Speech.Services.SpeechMessages` |
| Recorder JS seam | `BlazorBase.Speech.Interop.ISpeechRecorderInterop` / `SpeechRecorderInterop` |
| Shared contracts | `BlazorBase.Speech.Contracts` — `SpeechRoutes`, `TranscriptionResult`, `SpeechSynthesisRequest`, `SpeechAvailability`, `SpeechAudio` |
| DI registration | `AddBlazorBaseSpeech` |

---

## DI Registration

```csharp
// Client Program.cs
builder.Services.AddBlazorBaseSpeech();
builder.Services
    .AddHttpClient<ISpeechClient, HttpSpeechClient>(client =>
        client.BaseAddress = new Uri(new Uri(builder.HostEnvironment.BaseAddress), "api/speech/"))
    .AddHttpMessageHandler<AuthTokenHandler>();
```

The `HttpClient` is the host's to register, exactly like `BlazorBase.Files`: only the host knows the
proxy's address and its authentication handler. The trailing slash of the base address matters —
the client resolves `transcriptions`, `speech` and `availability` relative to it.

`AddBlazorBaseSpeech` registers `ISpeechRecorderInterop` (transient — one per button),
`ISpeechAudioPlayer`, `ISpeechNarrator`, `SpeechTextPreparer` and `SpeechMessages` (scoped), and
localization. Every registration uses `TryAdd`, so a host can replace any of them first.

---

## PushToTalkButton

```razor
<PushToTalkButton Disabled="@IsTranscribing"
                  OnRecordingStarted="@StopReadingAloudAsync"
                  OnRecorded="@TranscribeAsync" />
```

| Parameter | Default | Meaning |
|---|---|---|
| `Disabled` | `false` | Blocks recording, e.g. while the previous recording is processed. |
| `MaximumDurationSeconds` | `60` | Recording stops by itself after this long. |
| `MinimumDurationMilliseconds` | `300` | Shorter presses are discarded as accidental taps. |
| `ShowCaption` | `true` | Shows the hint next to the button; screen readers get it either way (`aria-describedby`). |
| `Class` | — | Extra classes on the wrapper. |
| `OnRecordingStarted` | — | Fires once the microphone records — the moment to stop anything being read aloud. |
| `OnRecorded` | — | Receives a `SpeechRecording(byte[] Wav, TimeSpan Duration)`. |

Behavior worth knowing:

- **Hold by pointer, touch, or Space/Enter** while focused. Pointer capture keeps the recording alive
  when the finger slides off the button; long-press side effects (scrolling, selection, the iOS
  callout, the context menu) are disabled on the button.
- **The microphone is opened per press and released per release.** Keeping it open would save the
  fraction of a second it takes to open, but Safari on iOS routes all audio to the earpiece while a
  microphone is open, which makes a spoken answer nearly inaudible.
- **Every press primes playback** (`speechPlayer.js` → `primePlayback`), because a press is the user
  gesture browsers require before audio may play.
- **Releasing while the microphone is still opening** (typically the first-time permission prompt)
  discards the press instead of recording nothing.
- **Recording format**: captured at the device rate through an `AudioWorklet`, box-filtered down to
  16 kHz (devices below 16 kHz keep their rate), encoded as 16-bit PCM mono WAV with a 44-byte header.
- **Level ring**: the recorder sets `--speech-level` (0..1) on the button while recording; the
  isolated CSS scales a ring with it. `--push-to-talk-size` (default `56px`) sizes the button.
- **Errors** (`PermissionDenied`, `NoMicrophone`, `Unsupported`, `Failed`) render as a localized
  `role="alert"` next to the button; the next successful press clears it.

The button styles itself from the theme contract with Fluent fallbacks (`--color-accent`,
`--color-text-on-accent`, `--color-danger`, `--color-text-on-danger`, `--color-border-focus`,
`--color-text-muted`).

---

## ISpeechNarrator

```csharp
await Narrator.SpeakAsync(answerMarkdown, language: "de", cancellationToken);
await Narrator.StopAsync();
```

- `SpeakAsync` stops whatever was being read, prepares the text with `SpeechTextPreparer`, and plays
  the segments in order. **While one segment plays, the next is already being synthesized**, so the
  pause between segments stays short without synthesizing a long answer up front.
- **The voice follows the text, not the UI.** Without an explicit `language`, `SpeechLanguageGuesser`
  tells German from English by counting function words that exist in only one of the two languages
  (too little evidence or a tie → `null`, and the UI culture decides). People answer and ask in the
  language they think in, and an English voice reading a German answer is barely intelligible.
- It completes when the last segment has played or when reading was stopped. A synthesis failure is
  thrown as `SpeechRequestException`; `IsSpeaking` is reset either way.
- `IsSpeaking` + `SpeakingChanged` let a UI show a stop control.

---

## SpeechTextPreparer

Turns chat-style Markdown into plain-text segments:

| Markdown | Spoken as |
|---|---|
| `**bold**`, `*italic*`, `~~strike~~`, headings, blockquotes, list markers, task boxes | the words only |
| `[text](url)`, `![alt](src)` | `text`, `alt` |
| bare or `<auto>` URLs | the localized word for "link" |
| `` `inline code` `` | its content |
| fenced code block (terminated or not) | one localized hint ("a code block follows, see the written answer") |
| table | one localized hint |

- Identifiers such as `my_variable_name` are not mistaken for emphasis.
- Sentences are split at `.`, `!`, `?`, `…` — but not after known abbreviations (`z. B.`, `bzw.`,
  `e.g.`, …), single letters or ordinals (`am 3. Oktober`). A line without final punctuation (list
  items, headings) gets a full stop, so the engine pauses there.
- The **first sentence is its own segment**, so playback starts early; later sentences are grouped up
  to `MaximumSegmentLength` (300) characters. Longer sentences are split at a comma, else at a space.
- Hints follow the current UI culture (`SpeechTextPreparer.resx` / `.de.resx`).

---

## ISpeechClient

| Member | Route (relative to the base address) | Result |
|---|---|---|
| `IsAvailableAsync()` | `GET availability` | `false` on any failure — including a non-JSON answer such as a SPA fallback page |
| `TranscribeAsync(wav, language?)` | `POST transcriptions` (multipart: `file`, `language`) | recognized text |
| `SynthesizeAsync(text, language?)` | `POST speech` (JSON `SpeechSynthesisRequest`) | WAV bytes |

**Language.** For `TranscribeAsync`, leave `language` `null` unless it is really known — the field is
then omitted and the speech service detects the spoken language itself. Do **not** pass the UI culture:
speech recognition treats the code as an instruction, not a hint, and *translates* into it (German
speech with `en` came back as an English sentence in testing, and vice versa). `SynthesizeAsync`
falls back to `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName`, because a voice has to be chosen.

Failures throw `SpeechRequestException` with a `SpeechFailureKind`:

| Status / condition | Kind |
|---|---|
| 429, 503 | `Busy` |
| 504, 408, `HttpClient` timeout | `Timeout` |
| 400, 413, 415 | `Rejected` |
| anything else, connection failure | `Unavailable` |

A caller's own cancellation stays an `OperationCanceledException`. `SpeechMessages.Describe(kind)` and
`Describe(SpeechRecorderError)` return the localized, user-facing explanation.

---

## Project Dependencies

```
BlazorBase.Speech   (standalone RCL — Components.Web, Extensions.Http, Extensions.Localization, FluentUI + Icons)
```
