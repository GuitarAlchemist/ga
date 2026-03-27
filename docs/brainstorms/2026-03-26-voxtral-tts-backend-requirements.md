---
date: 2026-03-26
topic: voxtral-tts-backend
---

# Voxtral TTS Backend Proxy for Demerzel Chat

## Problem Frame

The Demerzel chatbot in Prime Radiant uses the browser's Web Speech API for text-to-speech. Voice quality varies wildly across OS/browser combinations, voices can't be controlled server-side, and the experience is inconsistent. Mistral's Voxtral TTS (4B params, free weights, beats ElevenLabs in listener preference) provides a high-quality, consistent alternative.

## Requirements

- R1. Add a `POST /api/tts` endpoint in GaApi that accepts text and returns an audio blob (WAV or MP3).
- R2. The endpoint calls Mistral's Voxtral API (`mistralai/Voxtral-4B-TTS-2603`) using a server-side API key.
- R3. Use a Voxtral preset voice for Demerzel (configurable via app settings, not hardcoded).
- R4. ChatWidget.tsx plays the returned audio blob via `<audio>` / `AudioContext` instead of `speechSynthesis`.
- R5. If the TTS endpoint fails or is not configured (no API key), fall back gracefully to the existing Web Speech API.
- R6. The TTS toggle button in the chat header continues to work as before.

## Success Criteria

- Demerzel speaks with a consistent, high-quality voice regardless of OS/browser.
- TTS still works (via fallback) when Voxtral is not configured.
- No perceptible latency regression vs. current Web Speech API.

## Scope Boundaries

- No voice cloning — use preset voices only.
- No local vLLM hosting in this iteration — API-only. (Local can be swapped in later since the backend abstracts the provider.)
- No streaming audio — simple request/response is fine for chat-length utterances.
- No changes to speech recognition (input) — only speech output.

## Key Decisions

- **Backend proxy (Option C)**: API key stays server-side, all clients get the same voice, provider is swappable.
- **Preset voice**: Ship with a Voxtral preset; voice ID configurable in appsettings.
- **Graceful fallback**: Web Speech API remains as fallback when Voxtral is unavailable.

## Next Steps

→ `/ce:plan` for structured implementation planning
