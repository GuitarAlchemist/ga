/**
 * Modal Meadow — minimal Web Audio chord-pad engine.
 *
 * Why raw Web Audio (no Tone.js / Howler): neither library is in
 * package.json, and the task says don't add an audio dep in v0.
 *
 * Architecture:
 *  - 1 AudioContext
 *  - 1 masterGain → destination (sets overall ambient level ≈ -20 dB)
 *  - per mode: a `ChordBus` with its own gain node (lets us crossfade)
 *  - each bus owns the oscillators currently sounding its chord
 *  - a JS interval per bus advances to the next chord on a fixed cadence
 *
 * Chord voicing: each MIDI note plays sine + detuned triangle through a
 * gentle low-pass, with short attack and long release (soft pad). Volume
 * per voice scales 1/√(notes-in-chord) so triads don't clip.
 *
 * Browser autoplay policy: callers must `start()` after a user gesture
 * (we attach to canvas click in ModalMeadow.tsx). Before that we silently
 * do nothing.
 */

import type { ModeConfig } from './modes';

const MASTER_GAIN_DB = -20; // ambient — never foregrounded
const dbToGain = (db: number): number => Math.pow(10, db / 20);

const midiToHz = (midi: number): number => 440 * Math.pow(2, (midi - 69) / 12);

interface ChordVoice {
  /** All running OscillatorNodes for this chord. */
  oscillators: OscillatorNode[];
  /** Per-voice gain envelope, used to release on chord change. */
  voiceGain: GainNode;
}

interface ChordBus {
  /** Mix gain that the engine writes to for crossfade. */
  busGain: GainNode;
  /** Currently sounding voice; null between chords. */
  current: ChordVoice | null;
  /** Index into mode.progression for the next chord. */
  nextIndex: number;
  /** Timeout handle for the next chord advance. */
  timer: number | null;
}

/**
 * Chord-pulse event fired each time a chord starts on a mode bus.
 * Consumers (the visual grass shader) read this to animate a brief
 * sway pulse synchronised with the pad attack.
 *
 *   modeIndex   — 0 = Ionian, 1 = Phrygian (matches MODES array order)
 *   chordIndex  — index into the mode's progression (0..progression.length-1)
 *   romanRoot   — degree of the chord root (1 = tonic, 4 = subdominant,
 *                 5 = dominant, 2 = supertonic, etc.). Used by the
 *                 caller to pick a directional sway pattern.
 */
export interface ChordPulseEvent {
  modeIndex: number;
  chordIndex: number;
  /** Roman degree of this chord (1..7). Heuristic, see `degreeOfChord`. */
  romanRoot: number;
}

export type ChordPulseListener = (evt: ChordPulseEvent) => void;

/**
 * Cheap heuristic: read the *lowest* MIDI in the chord and map it back to
 * a scale-degree within the mode's tonic. v0 only uses Ionian (tonic C=60)
 * and Phrygian (tonic E=64) — both diatonic — so this is good enough to
 * drive a "tonic / subdominant / dominant" sway directional cue without
 * the caller needing a music-theory dependency.
 */
const TONIC_BY_MODE_INDEX: Record<number, number> = {
  0: 60, // Ionian tonic = C (60 mod 12 = 0)
  1: 64, // Phrygian tonic = E (64 mod 12 = 4)
};

const degreeOfChord = (modeIndex: number, chordMidi: number[]): number => {
  if (chordMidi.length === 0) return 1;
  const root = Math.min(...chordMidi);
  const tonic = TONIC_BY_MODE_INDEX[modeIndex] ?? 60;
  const semis = ((root - tonic) % 12 + 12) % 12;
  // Major scale degree → semitone offsets: 1→0, 2→2, 3→4, 4→5, 5→7, 6→9, 7→11.
  // Phrygian gets bII (offset 1) which we map to "subdominant-like" (degree 4)
  // because the brief says bII should pulse the +X side like IV.
  const table: Record<number, number> = {
    0: 1,  // I or i
    1: 4,  // bII → treat as subdominant per brief
    2: 2,
    3: 3,
    4: 3,  // iii / bIII
    5: 4,  // IV
    7: 5,  // V
    9: 6,
    10: 7, // bVII / bvii → treat as dominant per brief
    11: 7, // VII
  };
  return table[semis] ?? 1;
};

export class ModalMeadowAudio {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private buses: ChordBus[] = [];
  private modes: ModeConfig[] = [];
  private started = false;
  private chordPulseListeners: ChordPulseListener[] = [];

  /**
   * Subscribe to chord-pulse events (one per chord-start, on every bus).
   * Returns an unsubscribe function. Safe to call before `start()`.
   */
  onChordPulse(listener: ChordPulseListener): () => void {
    this.chordPulseListeners.push(listener);
    return () => {
      this.chordPulseListeners = this.chordPulseListeners.filter((l) => l !== listener);
    };
  }

  /**
   * Lazy-create AudioContext + buses for each mode. Safe to call repeatedly;
   * subsequent calls are no-ops. Must be triggered by a user gesture.
   */
  start(modes: ModeConfig[]): void {
    if (this.started) return;
    this.started = true;
    this.modes = modes;

    // Safari/iOS still needs the webkit prefix on older versions.
    const Ctor: typeof AudioContext =
      window.AudioContext ??
      (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    if (!Ctor) {
      console.warn('[ModalMeadowAudio] Web Audio not available, audio disabled.');
      return;
    }

    this.ctx = new Ctor();
    this.master = this.ctx.createGain();
    this.master.gain.value = dbToGain(MASTER_GAIN_DB);
    this.master.connect(this.ctx.destination);

    for (const mode of modes) {
      const busGain = this.ctx.createGain();
      busGain.gain.value = 0;             // start silent; setMix() opens it
      busGain.connect(this.master);
      const bus: ChordBus = {
        busGain,
        current: null,
        nextIndex: 0,
        timer: null,
      };
      this.buses.push(bus);
      this.advanceChord(bus, mode);
    }
  }

  /**
   * Set the relative gain of each mode bus. Weights should sum to ~1; we
   * scale by sqrt to keep perceived loudness roughly even across mixes
   * (equal-power crossfade). Smoothing avoids zipper noise.
   */
  setWeights(weights: number[]): void {
    if (!this.ctx) return;
    const now = this.ctx.currentTime;
    for (let i = 0; i < this.buses.length && i < weights.length; i++) {
      const target = Math.sqrt(Math.max(0, weights[i]));
      this.buses[i].busGain.gain.setTargetAtTime(target, now, 0.08);
    }
  }

  /** Stop everything and free resources. Idempotent. */
  dispose(): void {
    for (const bus of this.buses) {
      if (bus.timer !== null) {
        window.clearTimeout(bus.timer);
        bus.timer = null;
      }
      if (bus.current) {
        this.stopVoice(bus.current);
        bus.current = null;
      }
    }
    this.buses = [];
    if (this.ctx) {
      // Don't await — even ignored we want context torn down fast on unmount.
      void this.ctx.close();
      this.ctx = null;
    }
    this.master = null;
    this.started = false;
    this.chordPulseListeners = [];
  }

  /**
   * Stub-mode helper. When audio has not been started (no user gesture
   * yet) the caller can still drive visual pulses on a fake timer. This
   * returns a stop-callback so the caller can switch off the stub once
   * real audio kicks in. Visible-only: emits the same events.
   */
  startStubPulses(modes: ModeConfig[]): () => void {
    const handles: number[] = [];
    modes.forEach((mode, modeIndex) => {
      let i = 0;
      const tick = () => {
        const chordIndex = i % mode.progression.length;
        i += 1;
        const romanRoot = degreeOfChord(modeIndex, mode.progression[chordIndex]);
        for (const l of this.chordPulseListeners) {
          try {
            l({ modeIndex, chordIndex, romanRoot });
          } catch (err) {
            console.warn('[ModalMeadowAudio] stub-pulse listener threw', err);
          }
        }
        handles.push(window.setTimeout(tick, mode.chordDurationSec * 1000));
      };
      handles.push(window.setTimeout(tick, 100));
    });
    return () => {
      for (const h of handles) window.clearTimeout(h);
    };
  }

  // ─── Internals ─────────────────────────────────────────────────────────

  private advanceChord(bus: ChordBus, mode: ModeConfig): void {
    if (!this.ctx) return;
    const chordIndex = bus.nextIndex % mode.progression.length;
    const chord = mode.progression[chordIndex];
    bus.nextIndex += 1;

    if (bus.current) {
      this.stopVoice(bus.current);
    }
    bus.current = this.playChord(bus, chord);

    // Emit chord-pulse for the visual sway. modeIndex = position of this bus
    // in `this.buses` (which mirrors `modes` argument order to start()).
    const modeIndex = this.buses.indexOf(bus);
    const romanRoot = degreeOfChord(modeIndex, chord);
    for (const l of this.chordPulseListeners) {
      try {
        l({ modeIndex, chordIndex, romanRoot });
      } catch (err) {
        console.warn('[ModalMeadowAudio] chord-pulse listener threw', err);
      }
    }

    bus.timer = window.setTimeout(() => {
      this.advanceChord(bus, mode);
    }, mode.chordDurationSec * 1000);
  }

  private playChord(bus: ChordBus, midi: number[]): ChordVoice {
    if (!this.ctx) return { oscillators: [], voiceGain: bus.busGain };
    const now = this.ctx.currentTime;
    const voiceGain = this.ctx.createGain();
    voiceGain.gain.value = 0;
    // Soft pad envelope: attack 0.4s, hold, release at stopVoice.
    voiceGain.gain.linearRampToValueAtTime(1.0 / Math.sqrt(midi.length), now + 0.4);
    voiceGain.connect(bus.busGain);

    // Low-pass to take the harshness off the triangle harmonics.
    const lp = this.ctx.createBiquadFilter();
    lp.type = 'lowpass';
    lp.frequency.value = 1200;
    lp.Q.value = 0.7;
    lp.connect(voiceGain);

    const oscillators: OscillatorNode[] = [];
    for (const m of midi) {
      const hz = midiToHz(m);
      // Sine — the body of the chord.
      const sine = this.ctx.createOscillator();
      sine.type = 'sine';
      sine.frequency.value = hz;
      sine.connect(lp);
      sine.start(now);
      oscillators.push(sine);

      // Triangle one cent flat — adds a beat/chorus shimmer at no CPU cost.
      const tri = this.ctx.createOscillator();
      tri.type = 'triangle';
      tri.frequency.value = hz;
      tri.detune.value = -7;
      tri.connect(lp);
      tri.start(now);
      oscillators.push(tri);
    }

    return { oscillators, voiceGain };
  }

  private stopVoice(voice: ChordVoice): void {
    if (!this.ctx) return;
    const now = this.ctx.currentTime;
    // Quick release; sounds like a pad fading out instead of a click.
    voice.voiceGain.gain.cancelScheduledValues(now);
    voice.voiceGain.gain.setValueAtTime(voice.voiceGain.gain.value, now);
    voice.voiceGain.gain.linearRampToValueAtTime(0, now + 0.35);
    for (const osc of voice.oscillators) {
      osc.stop(now + 0.4);
    }
  }
}
