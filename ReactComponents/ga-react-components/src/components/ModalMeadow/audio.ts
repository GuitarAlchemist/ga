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

export class ModalMeadowAudio {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private buses: ChordBus[] = [];
  private modes: ModeConfig[] = [];
  private started = false;

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
  }

  // ─── Internals ─────────────────────────────────────────────────────────

  private advanceChord(bus: ChordBus, mode: ModeConfig): void {
    if (!this.ctx) return;
    const chord = mode.progression[bus.nextIndex % mode.progression.length];
    bus.nextIndex += 1;

    if (bus.current) {
      this.stopVoice(bus.current);
    }
    bus.current = this.playChord(bus, chord);

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
