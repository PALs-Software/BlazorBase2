# BlazorBase.Speech.Server

Server-side ASP.NET Core library (net10.0, `Microsoft.AspNetCore.App`) with an abstract, authenticated
proxy controller and a typed client for an internal speech service. It is the server companion to
**BlazorBase.Speech**.

The speech service is expected to speak OpenAI-shaped audio routes — `POST /v1/audio/transcriptions`
(multipart `file` + `language`, answers `{ "text": … }`) and `POST /v1/audio/speech` (JSON
`{ "input": …, "language": … }`, answers WAV). The PAL server runs such a service as the internal
`speech-api` container (repository `Pipelines/SpeechApi`) in front of Wyoming Whisper and Piper
engines. It has **no authentication** — network isolation is its boundary — so browsers must never
reach it directly; this proxy is the only way in.

---

## Overview

| Concern | Type |
|---|---|
| Abstract REST controller | `BlazorBase.Speech.Server.Controllers.SpeechControllerBase` |
| Speech service client | `BlazorBase.Speech.Server.Services.ISpeechApiClient` / `SpeechApiClient` |
| Per-user request cap | `BlazorBase.Speech.Server.Services.SpeechRequestGate` |
| Options + validation | `BlazorBase.Speech.Server.Configuration.SpeechServerOptions` / `SpeechServerOptionsValidator` |
| Localized problem details | `BlazorBase.Speech.Server.Localization.SpeechServerText` (+ `.resx`) |
| DI registration | `AddBlazorBaseSpeechServer` |

---

## DI Registration

```csharp
// Server Program.cs
builder.Services.AddBlazorBaseSpeechServer(builder.Configuration);
```

```json
{
  "Speech": {
    "BaseUrl": "http://speech-api:8080"
  }
}
```

| Option | Default | Meaning |
|---|---|---|
| `BaseUrl` | — | Absolute http(s) address of the speech service. **Empty switches speech off** — the proxy reports itself unavailable instead of failing at startup, so a development machine without the service still boots. |
| `TimeoutSeconds` | `120` | `HttpClient` timeout per transcription or synthesis. |
| `MaxUploadBytes` | 10 MiB | Largest accepted recording (~5 min of 16 kHz mono). |
| `MaxTextCharacters` | `4000` | Longest text per synthesis request. |
| `MaxConcurrentRequestsPerUser` | `2` | Requests one user may have in flight (one transcription plus one synthesis, or a synthesis plus the prefetch of the next segment). |

Options are validated on start: a set `BaseUrl` must be an absolute http or https URL, and every limit
must be positive.

---

## Abstract Controller

Derive a thin concrete controller; the base already carries `[ApiController]`,
`[Route("api/speech")]` and `[Authorize]`.

```csharp
public class SpeechController(
    ISpeechApiClient speechApiClient,
    SpeechRequestGate requestGate,
    IOptions<SpeechServerOptions> options,
    IStringLocalizer<SpeechServerText> localizer)
    : SpeechControllerBase(speechApiClient, requestGate, options, localizer);
```

Override `ResolveUserKey()` when users are identified by something other than the
`ClaimTypes.NameIdentifier` claim (fallbacks: identity name, then connection id).

---

## REST Endpoints

| Method | Route | Body | Success | Failures |
|---|---|---|---|---|
| `GET` | `/api/speech/availability` | — | `SpeechAvailability` | — |
| `POST` | `/api/speech/transcriptions` | multipart: `file` (WAV), `language` | `TranscriptionResult` | 400 missing/empty, 413 too large, 415 no RIFF/WAVE signature |
| `POST` | `/api/speech/speech` | JSON `SpeechSynthesisRequest` | `audio/wav` | 400 empty or too long |

Common to both `POST` routes:

| Status | When |
|---|---|
| 503 | speech not configured, or the service is busy (its queue is full) |
| 429 | the user is at `MaxConcurrentRequestsPerUser` |
| 504 | the service did not answer in time |
| 502 | the service is unreachable or failed |
| 400 | the service rejected the request |

Every failure is an RFC 7807 problem with a **localized `detail`** (`SpeechServerText.resx` /
`.de.resx`, following the request's UI culture). The language is normalized before it is forwarded:
`de-DE`, `DE` and `de_de` become `de`; anything that is not a 2–3 letter tag is dropped, leaving the
service's default.

Recordings are checked for size and a RIFF/WAVE signature before they leave the host. Neither
recordings nor texts are logged or stored.

---

## Kestrel / Form Limits

The default Kestrel request body limit (30 MB) and multipart limit (128 MB) exceed the default
`MaxUploadBytes`, so nothing needs changing unless `MaxUploadBytes` is raised beyond them.

---

## Project Dependencies

```
BlazorBase.Speech.Server
  └─ BlazorBase.Speech   (shared contracts, SpeechRequestException, SpeechFailureKind)
```
