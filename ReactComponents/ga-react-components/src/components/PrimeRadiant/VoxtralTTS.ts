// src/components/PrimeRadiant/VoxtralTTS.ts
// Standalone Demerzel voice pipeline — Voxtral TTS with Web Speech API fallback.
// Decoupled from ChatWidget so any component can trigger Demerzel speech.

import { useState, useCallback, useRef, useEffect } from 'react';
import { setDemerzelSpeaking, setDemerzelEmotion } from './GodotScene';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface TTSRequest {
  text: string;
  voiceId?: string;
}

export interface TTSResponse {
  audioBase64: string;
  durationMs: number;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

/** Demerzel's Voxtral voice — Oliver */
const DEMERZEL_VOICE_ID = 'e3596645-b1af-469e-b857-f18ddedc7652';

/** Proxy endpoint — keeps MISTRAL_API_KEY server-side */
const VOXTRAL_PROXY_PATH = '/proxy/voxtral/v1/audio/speech';

// ---------------------------------------------------------------------------
// Internal state (module-level singleton for playback control)
// ---------------------------------------------------------------------------

let activeAudioContext: AudioContext | null = null;
let activeSource: AudioBufferSourceNode | null = null;
let activeBlobAudio: HTMLAudioElement | null = null;
let activeBlobUrl: string | null = null;
let currentAbortController: AbortController | null = null;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function getProxyBaseUrl(): string {
  if (typeof import.meta !== 'undefined') {
    const env = (import.meta as { env?: Record<string, string> }).env;
    return env?.VITE_API_BASE_URL ?? '';
  }
  return '';
}

/** Decode base64 string to ArrayBuffer */
function base64ToArrayBuffer(base64: string): ArrayBuffer {
  const binaryString = atob(base64);
  const len = binaryString.length;
  const bytes = new Uint8Array(len);
  for (let i = 0; i < len; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  return bytes.buffer;
}

/** Estimate speech duration from text length (fallback when not provided) */
function estimateDurationMs(text: string): number {
  // Rough estimate: ~150 words/minute, ~5 chars/word
  const words = text.length / 5;
  return Math.max(1000, (words / 150) * 60 * 1000);
}

// ---------------------------------------------------------------------------
// Core: stop any current playback
// ---------------------------------------------------------------------------

export function stopSpeaking(): void {
  // Cancel in-flight fetch
  if (currentAbortController) {
    currentAbortController.abort();
    currentAbortController = null;
  }

  // Stop AudioContext source
  if (activeSource) {
    try { activeSource.stop(); } catch { /* already stopped */ }
    activeSource = null;
  }
  if (activeAudioContext) {
    try { void activeAudioContext.close(); } catch { /* ignore */ }
    activeAudioContext = null;
  }

  // Stop HTMLAudioElement (blob-based playback)
  if (activeBlobAudio) {
    activeBlobAudio.pause();
    activeBlobAudio.src = '';
    activeBlobAudio = null;
  }
  if (activeBlobUrl) {
    URL.revokeObjectURL(activeBlobUrl);
    activeBlobUrl = null;
  }

  // Stop browser speech synthesis
  if (typeof speechSynthesis !== 'undefined') {
    speechSynthesis.cancel();
  }

  // Reset Godot face
  setDemerzelSpeaking(false);
  setDemerzelEmotion('neutral');
}

// ---------------------------------------------------------------------------
// Fallback: Web Speech API
// ---------------------------------------------------------------------------

function speakWithWebSpeech(text: string): Promise<void> {
  return new Promise<void>((resolve) => {
    if (typeof speechSynthesis === 'undefined') {
      resolve();
      return;
    }

    speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.rate = 0.9;
    utterance.pitch = 0.85;

    // Pick a reasonable English voice
    const voices = speechSynthesis.getVoices();
    const preferred =
      voices.find((v) => v.name.includes('Google UK English Female')) ??
      voices.find((v) => v.name.includes('Samantha')) ??
      voices.find((v) => v.lang.startsWith('en') && !v.localService) ??
      voices.find((v) => v.lang.startsWith('en'));
    if (preferred) utterance.voice = preferred;

    utterance.onend = () => resolve();
    utterance.onerror = () => resolve();

    // Safety timeout — never hang forever
    const timeout = setTimeout(() => {
      speechSynthesis.cancel();
      resolve();
    }, estimateDurationMs(text) + 3000);

    utterance.onend = () => { clearTimeout(timeout); resolve(); };
    utterance.onerror = () => { clearTimeout(timeout); resolve(); };

    speechSynthesis.speak(utterance);
  });
}

// ---------------------------------------------------------------------------
// Primary: play base64 audio via AudioContext
// ---------------------------------------------------------------------------

async function playBase64Audio(audioBase64: string): Promise<void> {
  const arrayBuffer = base64ToArrayBuffer(audioBase64);
  const ctx = new AudioContext();
  activeAudioContext = ctx;

  const audioBuffer = await ctx.decodeAudioData(arrayBuffer);
  const source = ctx.createBufferSource();
  activeSource = source;
  source.buffer = audioBuffer;
  source.connect(ctx.destination);

  return new Promise<void>((resolve) => {
    source.onended = () => {
      activeSource = null;
      void ctx.close().then(() => { activeAudioContext = null; });
      resolve();
    };
    source.start(0);
  });
}

// ---------------------------------------------------------------------------
// Primary: play audio blob via HTMLAudioElement (fallback for raw blob response)
// ---------------------------------------------------------------------------

async function playAudioBlob(blob: Blob): Promise<void> {
  const url = URL.createObjectURL(blob);
  activeBlobUrl = url;

  const audio = new Audio(url);
  activeBlobAudio = audio;

  return new Promise<void>((resolve) => {
    audio.onended = () => {
      URL.revokeObjectURL(url);
      activeBlobUrl = null;
      activeBlobAudio = null;
      resolve();
    };
    audio.onerror = () => {
      URL.revokeObjectURL(url);
      activeBlobUrl = null;
      activeBlobAudio = null;
      resolve();
    };
    void audio.play().catch(() => resolve());
  });
}

// ---------------------------------------------------------------------------
// Main entry: speakAsDemerzel
// ---------------------------------------------------------------------------

export async function speakAsDemerzel(text: string): Promise<void> {
  if (!text.trim()) return;

  // Cancel any in-progress speech
  stopSpeaking();

  // Signal Godot face
  setDemerzelSpeaking(true);
  setDemerzelEmotion('speaking');

  const abortController = new AbortController();
  currentAbortController = abortController;

  try {
    // --- Strategy 1: Voxtral proxy (JSON response with audio_data) ---
    const baseUrl = getProxyBaseUrl();
    const proxyUrl = baseUrl
      ? `${baseUrl}${VOXTRAL_PROXY_PATH}`
      : VOXTRAL_PROXY_PATH;

    const response = await fetch(proxyUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        model: 'voxtral-mini-tts-2603',
        input: text,
        voice_id: DEMERZEL_VOICE_ID,
      } satisfies { model: string; input: string; voice_id: string }),
      signal: abortController.signal,
    });

    if (!response.ok) {
      throw new Error(`Voxtral proxy returned ${response.status}`);
    }

    // The Voxtral API may return JSON with audio_data, or raw audio blob
    const contentType = response.headers.get('content-type') ?? '';

    if (contentType.includes('application/json')) {
      const data = await response.json() as { audio_data?: string };
      if (data.audio_data) {
        await playBase64Audio(data.audio_data);
      } else {
        throw new Error('No audio_data in response');
      }
    } else {
      // Raw audio blob (e.g., audio/wav, audio/mp3)
      const blob = await response.blob();
      await playAudioBlob(blob);
    }
  } catch (err: unknown) {
    // If aborted, exit silently
    if (err instanceof DOMException && err.name === 'AbortError') return;

    // --- Strategy 2: GaApi /api/tts endpoint ---
    try {
      const apiBase = getProxyBaseUrl() || (typeof window !== 'undefined' ? window.location.origin : '');
      const ttsResponse = await fetch(`${apiBase}/api/tts`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text }),
        signal: abortController.signal,
      });

      if (ttsResponse.ok) {
        const blob = await ttsResponse.blob();
        await playAudioBlob(blob);
        return;
      }
    } catch {
      // Fall through to Web Speech API
    }

    // --- Strategy 3: Web Speech API fallback ---
    await speakWithWebSpeech(text);
  } finally {
    currentAbortController = null;
    setDemerzelSpeaking(false);
    setDemerzelEmotion('neutral');
  }
}

// ---------------------------------------------------------------------------
// Availability check
// ---------------------------------------------------------------------------

/** Check if any TTS backend is available (at minimum Web Speech API) */
export function isTTSAvailable(): boolean {
  return typeof speechSynthesis !== 'undefined' || typeof AudioContext !== 'undefined';
}

// ---------------------------------------------------------------------------
// React Hook: useDemerzelVoice
// ---------------------------------------------------------------------------

export interface DemerzelVoiceHook {
  /** Speak text as Demerzel (auto-cancels previous) */
  speak: (text: string) => Promise<void>;
  /** Stop current speech */
  stop: () => void;
  /** Whether Demerzel is currently speaking */
  speaking: boolean;
  /** Whether any TTS backend is available */
  available: boolean;
}

export function useDemerzelVoice(): DemerzelVoiceHook {
  const [speaking, setSpeaking] = useState(false);
  const [available] = useState(() => isTTSAvailable());
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      stopSpeaking();
    };
  }, []);

  const speak = useCallback(async (text: string) => {
    if (!available) return;
    setSpeaking(true);
    try {
      await speakAsDemerzel(text);
    } finally {
      if (mountedRef.current) {
        setSpeaking(false);
      }
    }
  }, [available]);

  const stop = useCallback(() => {
    stopSpeaking();
    if (mountedRef.current) {
      setSpeaking(false);
    }
  }, []);

  return { speak, stop, speaking, available };
}
