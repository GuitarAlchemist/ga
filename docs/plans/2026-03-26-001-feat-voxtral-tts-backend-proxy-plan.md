---
title: "feat: Add Voxtral TTS backend proxy for Demerzel chat"
type: feat
status: active
date: 2026-03-26
origin: docs/brainstorms/2026-03-26-voxtral-tts-backend-requirements.md
---

# feat: Add Voxtral TTS backend proxy for Demerzel chat

## Overview

Replace the browser's Web Speech API in the Demerzel chatbot with a backend TTS proxy that calls Mistral's Voxtral API. The backend holds the API key, returns audio blobs, and the frontend plays them. Web Speech API remains as a graceful fallback.

## Problem Frame

The Demerzel chatbot uses `speechSynthesis` (Web Speech API) for TTS. Voice quality varies by OS/browser, voices can't be controlled server-side, and the experience is inconsistent across platforms. Voxtral TTS provides consistent, high-quality speech via a simple REST API. (see origin: docs/brainstorms/2026-03-26-voxtral-tts-backend-requirements.md)

## Requirements Trace

- R1. `POST /api/tts` endpoint in GaApi — accepts text, returns audio blob
- R2. Endpoint calls Mistral Voxtral API with server-side API key
- R3. Voice configurable via appsettings (preset voice ID, not hardcoded)
- R4. ChatWidget plays audio blob via `Audio` element instead of `speechSynthesis`
- R5. Graceful fallback to Web Speech API when endpoint fails or is unconfigured
- R6. TTS toggle button works as before

## Scope Boundaries

- No voice cloning — preset voices only
- No local vLLM hosting — API-only (swappable later)
- No streaming audio — simple request/response
- No changes to speech recognition (input)

## Context & Research

### Relevant Code and Patterns

- **Controller pattern**: `ChatbotController.cs` — `[ApiController]`, `[Route("api/[controller]")]`, primary constructor DI
- **Named HttpClient**: `AIServiceExtensions.cs` — `services.AddHttpClient("Ollama", ...)` pattern
- **Configuration**: `appsettings.json` — named sections like `"Ollama": { "BaseUrl": ... }`
- **Frontend API calls**: `ChatWidget.tsx` — `fetch()` with base URL `https://localhost:7001`
- **Service registration**: `Program.cs` — `builder.Services.AddAiServices(builder.Configuration)`
- **Options pattern**: `ChatbotOptions` used via `IOptionsMonitor<T>`

### External References

- Mistral Voxtral API docs: `https://docs.mistral.ai/capabilities/audio/text_to_speech`
- REST endpoint: `POST https://api.mistral.ai/v1/audio/speech`
- Request body: `{ "model": "mistralai/Voxtral-4B-TTS-2603", "input": "text", "voice": "preset_id", "response_format": "mp3" }`
- Response: raw audio bytes with appropriate content-type

## Key Technical Decisions

- **Named HttpClient "MistralTts"**: Follows the existing Ollama/DockerModelRunner pattern in `AIServiceExtensions.cs`
- **Options class `VoxtralTtsOptions`**: Holds API key, base URL, model, voice, response format. Bound from `appsettings.json` section `"VoxtralTts"`
- **Return `audio/mpeg` blob**: MP3 is compact and universally playable. No transcoding needed.
- **No streaming**: Chat responses are short (typically < 500 chars). A single request/response with ~90ms TTFA is fast enough.
- **Fallback detection**: Frontend tries `/api/tts` first; on any failure (network, 503, 500), falls back to `speechSynthesis`.

## Open Questions

### Resolved During Planning

- **Which Voxtral preset voice?** Start with a neutral English preset. The voice ID is configurable in appsettings, so it can be changed without code changes. The exact preset ID will be selected during implementation by checking Voxtral's available voices.
- **Audio format?** MP3 — small, universally supported, no transcoding.
- **Rate limiting?** Reuse the existing rate limiter in Program.cs. TTS calls are 1:1 with chat responses, so the existing chatbot rate limit covers it.

### Deferred to Implementation

- **Exact Voxtral preset voice IDs**: Need to query the API or docs for available preset names during implementation.
- **Audio caching**: Not in scope now, but the endpoint structure supports adding response caching later.

## Implementation Units

- [ ] **Unit 1: VoxtralTtsOptions and configuration**

  **Goal:** Add the options class and appsettings configuration for Voxtral TTS.

  **Requirements:** R2, R3

  **Dependencies:** None

  **Files:**
  - Create: `Apps/ga-server/GaApi/Options/VoxtralTtsOptions.cs`
  - Modify: `Apps/ga-server/GaApi/appsettings.json`
  - Modify: `Apps/ga-server/GaApi/appsettings.Development.json`

  **Approach:**
  - Record with `ApiKey`, `BaseUrl` (default `https://api.mistral.ai`), `Model` (default `mistralai/Voxtral-4B-TTS-2603`), `Voice` (default preset), `ResponseFormat` (default `mp3`)
  - Bind from `"VoxtralTts"` section in appsettings
  - API key blank by default — endpoint returns 503 when unconfigured

  **Patterns to follow:**
  - `ChatbotOptions` in `Apps/ga-server/GaApi/Options/` for options class style
  - `"Ollama"` section in appsettings.json for configuration layout

  **Test scenarios:**
  - Options bind correctly from configuration
  - Defaults are sensible when section is missing

  **Verification:**
  - Solution builds. Options class is resolvable from DI.

- [ ] **Unit 2: TTS service and HttpClient registration**

  **Goal:** Add `IVoxtralTtsService` and its implementation that calls the Voxtral API, plus named HttpClient registration.

  **Requirements:** R1, R2

  **Dependencies:** Unit 1

  **Files:**
  - Create: `Apps/ga-server/GaApi/Services/IVoxtralTtsService.cs`
  - Create: `Apps/ga-server/GaApi/Services/VoxtralTtsService.cs`
  - Modify: `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs`

  **Approach:**
  - Interface: `Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct)`
  - Implementation: uses named HttpClient `"MistralTts"`, posts to `/v1/audio/speech`, returns raw bytes
  - Returns `null` when API key is not configured (allows controller to signal fallback)
  - Register named HttpClient and service in `AIServiceExtensions.AddAiServices()`

  **Patterns to follow:**
  - `OllamaChatService` — constructor takes `IHttpClientFactory`, `IConfiguration`, `IOptionsMonitor<T>`, `ILogger`
  - Named HttpClient registration in `AIServiceExtensions.cs`

  **Test scenarios:**
  - Service returns audio bytes on successful API call
  - Service returns null when API key is empty/missing
  - Service handles HTTP errors gracefully (logs, returns null)
  - Service respects cancellation token

  **Verification:**
  - Solution builds. Service is registered in DI. Unit tests pass.

- [ ] **Unit 3: TTS controller endpoint**

  **Goal:** Add `POST /api/tts` endpoint that accepts text and returns audio.

  **Requirements:** R1, R2, R3

  **Dependencies:** Unit 2

  **Files:**
  - Create: `Apps/ga-server/GaApi/Controllers/TtsController.cs`
  - Test: `Tests/Apps/GaApi.Tests/Controllers/TtsControllerTests.cs`

  **Approach:**
  - `[ApiController]`, `[Route("api/[controller]")]`
  - `[HttpPost]` accepts a request DTO with `Text` property
  - Calls `IVoxtralTtsService.SynthesizeAsync()`
  - Returns `File(bytes, "audio/mpeg")` on success
  - Returns `StatusCode(503)` with a JSON body `{ "fallback": true }` when service is unconfigured or fails — frontend uses this signal to fall back
  - Input validation: reject empty text, cap text length at a reasonable limit (e.g., 5000 chars)

  **Patterns to follow:**
  - `ChatbotController.cs` — controller structure, primary constructor, DI injection

  **Test scenarios:**
  - Returns audio bytes with `audio/mpeg` content type for valid text
  - Returns 503 with fallback signal when API key not configured
  - Returns 400 for empty or missing text
  - Returns 400 for text exceeding max length
  - Handles service errors gracefully

  **Verification:**
  - Integration test confirms endpoint responds correctly. Solution builds.

- [ ] **Unit 4: ChatWidget frontend — play audio from backend**

  **Goal:** Replace `speechSynthesis` calls with backend TTS, keeping Web Speech API as fallback.

  **Requirements:** R4, R5, R6

  **Dependencies:** Unit 3

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ChatWidget.tsx`

  **Approach:**
  - Replace `speakText` callback:
    1. `fetch('/api/tts', { method: 'POST', body: JSON.stringify({ text }), headers: { 'Content-Type': 'application/json' } })`
    2. If response ok: create `Audio` object from blob URL, play it
    3. If response fails (503, network error, etc.): fall back to existing `speechSynthesis` code
  - Keep the existing voice-selection and `speechSynthesis` code as the fallback path
  - TTS toggle button (`ttsEnabled` state) continues to gate both paths
  - Cancel any playing audio on new message or unmount (similar to current `speechSynthesis.cancel()`)
  - Use the GaApi base URL already used by other API calls in the widget

  **Patterns to follow:**
  - `askClaudeStreaming` in ChatWidget.tsx — fetch pattern with error handling
  - Existing `speakText` callback structure

  **Test scenarios:**
  - Audio plays from backend when TTS is enabled and endpoint succeeds
  - Falls back to speechSynthesis when endpoint returns 503
  - Falls back to speechSynthesis on network error
  - TTS toggle disables/enables both paths
  - Previous audio stops when new message arrives
  - Audio cleanup on component unmount

  **Verification:**
  - Frontend builds and lints clean. Manual test: TTS plays Voxtral audio in chat. Disabling API key triggers fallback to browser voice.

## System-Wide Impact

- **Interaction graph:** TTS controller is a new leaf endpoint — no callbacks, no middleware beyond existing rate limiter and CORS.
- **Error propagation:** Service returns null on failure → controller returns 503 → frontend falls back. No exceptions cross boundaries.
- **State lifecycle risks:** Audio blob URLs must be revoked after playback to avoid memory leaks. Existing `speechSynthesis.cancel()` pattern needs equivalent for `Audio` element.
- **API surface parity:** No other clients consume TTS yet. If SignalR chat adds TTS later, the service layer is reusable.
- **Integration coverage:** The GaApi.Tests integration test for the TTS endpoint covers the backend path. Frontend fallback behavior is manual-test only in this iteration.

## Risks & Dependencies

- **Mistral API availability**: If the API is down or rate-limited, users fall back to Web Speech API. No hard dependency.
- **API key provisioning**: Needs a Mistral API key in appsettings or environment. Document in README or appsettings comments.
- **Audio blob memory**: Must revoke object URLs after playback to prevent leaks in long chat sessions.

## Sources & References

- **Origin document:** [docs/brainstorms/2026-03-26-voxtral-tts-backend-requirements.md](docs/brainstorms/2026-03-26-voxtral-tts-backend-requirements.md)
- Related code: `ChatbotController.cs`, `OllamaChatService.cs`, `AIServiceExtensions.cs`, `ChatWidget.tsx`
- External docs: https://docs.mistral.ai/capabilities/audio/text_to_speech
