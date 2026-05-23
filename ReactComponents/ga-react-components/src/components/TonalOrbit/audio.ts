/**
 * TonalOrbit — Web Audio drone engine.
 *
 * One voice = one focused body. As the user focuses pitch → chord → scale,
 * the drone gains harmonic depth:
 *
 *   - Focused PITCH only: single root oscillator with octave doubling,
 *     gentle sine fundamental + triangle two octaves up.
 *   - Focused CHORD: also add the chord's third/fifth/seventh as soft
 *     pad voices.
 *   - Focused SCALE: ambient texture stays at the chord level — the
 *     scale itself isn't audible (you'd be playing melody, not pad), but
 *     the engine raises a per-scale "shimmer" partial tuned to the
 *     scale's characteristic note (the one most distant from the parent
 *     chord) so the user hears what makes the scale's flavour.
 *
 * Mirrors the raw-Web-Audio house style of ModalMeadow/audio.ts — no
 * Tone.js, ADSR envelopes, low-pass colour, fake-reverb send. Public API
 * is intentionally small so the React side can drive it without
 * understanding the internals.
 */

export interface DroneState {
  rootMidi: number | null;     // 0 = no root, otherwise MIDI note
  chordIntervals: number[] | null; // semitones above root; null = pitch-only
  shimmerSemitone: number | null;  // optional scale "flavour" pitch
}

const MASTER_GAIN_DB = -22;
const dbToGain = (db: number): number => Math.pow(10, db / 20);
const midiToHz = (midi: number): number => 440 * Math.pow(2, (midi - 69) / 12);

const FADE_IN_SEC = 0.4;
const FADE_OUT_SEC = 0.6;

interface Voice {
  oscillators: OscillatorNode[];
  envelope: GainNode;
}

export class TonalOrbitAudio {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private reverbBus: GainNode | null = null;

  /** Voices for each "layer" of the drone, keyed by role. */
  private voices = new Map<string, Voice>();
  private state: DroneState = { rootMidi: null, chordIntervals: null, shimmerSemitone: null };

  /** Lazily construct the audio graph; safe to call multiple times. */
  start(): void {
    if (this.ctx) return;
    const Ctor = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    if (!Ctor) {
      console.warn('[TonalOrbitAudio] AudioContext not available');
      return;
    }
    this.ctx = new Ctor();

    this.master = this.ctx.createGain();
    this.master.gain.value = dbToGain(MASTER_GAIN_DB);

    // Master low-pass to keep the drone soft / ambient.
    const lp = this.ctx.createBiquadFilter();
    lp.type = 'lowpass';
    lp.frequency.value = 1800;
    lp.Q.value = 0.7;

    // Fake reverb — feedback delay tap, low-pass filtered.
    const delay = this.ctx.createDelay(1.0);
    delay.delayTime.value = 0.22;
    const fb = this.ctx.createGain();
    fb.gain.value = 0.42;
    const dryWet = this.ctx.createGain();
    dryWet.gain.value = 0.28;
    delay.connect(fb).connect(delay);
    delay.connect(dryWet).connect(this.master);

    this.reverbBus = this.ctx.createGain();
    this.reverbBus.gain.value = 1.0;
    this.reverbBus.connect(delay);
    this.reverbBus.connect(lp);
    lp.connect(this.master);
    this.master.connect(this.ctx.destination);

    // Resume the context if it was suspended (autoplay policy).
    if (this.ctx.state === 'suspended') {
      this.ctx.resume().catch((err) => console.debug('[TonalOrbitAudio] resume failed', err));
    }
  }

  /**
   * Set the drone state. Internally diffs against the previous state
   * and only rebuilds the layers that changed.
   */
  setState(next: DroneState): void {
    if (!this.ctx) return;
    const prev = this.state;
    this.state = next;

    // ROOT layer
    if (next.rootMidi !== prev.rootMidi) {
      this.killVoice('root');
      if (next.rootMidi !== null) this.buildRoot(next.rootMidi);
    }

    // CHORD layer
    const chordChanged =
      JSON.stringify(next.chordIntervals ?? []) !== JSON.stringify(prev.chordIntervals ?? []) ||
      next.rootMidi !== prev.rootMidi;
    if (chordChanged) {
      this.killVoice('chord');
      if (next.rootMidi !== null && next.chordIntervals && next.chordIntervals.length > 1) {
        this.buildChord(next.rootMidi, next.chordIntervals);
      }
    }

    // SHIMMER layer
    if (next.shimmerSemitone !== prev.shimmerSemitone || next.rootMidi !== prev.rootMidi) {
      this.killVoice('shimmer');
      if (next.rootMidi !== null && next.shimmerSemitone !== null) {
        this.buildShimmer(next.rootMidi + next.shimmerSemitone);
      }
    }
  }

  /** Stop everything and tear down the context. */
  dispose(): void {
    for (const key of Array.from(this.voices.keys())) this.killVoice(key);
    this.voices.clear();
    if (this.ctx) {
      this.ctx.close().catch(() => undefined);
      this.ctx = null;
      this.master = null;
      this.reverbBus = null;
    }
  }

  // ─── Internal voice builders ──────────────────────────────────────────

  private buildRoot(midi: number): void {
    if (!this.ctx || !this.reverbBus) return;
    const env = this.ctx.createGain();
    env.gain.value = 0;
    env.connect(this.reverbBus);

    const oscs: OscillatorNode[] = [];

    // Fundamental — sine, drops one octave for warmth.
    const o1 = this.ctx.createOscillator();
    o1.type = 'sine';
    o1.frequency.value = midiToHz(midi - 12);
    const g1 = this.ctx.createGain();
    g1.gain.value = 0.6;
    o1.connect(g1).connect(env);
    o1.start();
    oscs.push(o1);

    // Octave-up triangle for shimmer.
    const o2 = this.ctx.createOscillator();
    o2.type = 'triangle';
    o2.frequency.value = midiToHz(midi);
    o2.detune.value = -4;
    const g2 = this.ctx.createGain();
    g2.gain.value = 0.25;
    o2.connect(g2).connect(env);
    o2.start();
    oscs.push(o2);

    this.fadeIn(env, 1.0);
    this.voices.set('root', { oscillators: oscs, envelope: env });
  }

  private buildChord(rootMidi: number, intervals: number[]): void {
    if (!this.ctx || !this.reverbBus) return;
    const env = this.ctx.createGain();
    env.gain.value = 0;
    env.connect(this.reverbBus);

    const oscs: OscillatorNode[] = [];
    // Skip the root (intervals[0] = 0), it's already in the root voice.
    for (let i = 1; i < intervals.length; i++) {
      const o = this.ctx.createOscillator();
      o.type = 'sine';
      o.frequency.value = midiToHz(rootMidi + intervals[i]);
      o.detune.value = (i - 1) * 3; // tiny detune spread for chorus feel
      const g = this.ctx.createGain();
      g.gain.value = 0.22;
      o.connect(g).connect(env);
      o.start();
      oscs.push(o);
    }
    this.fadeIn(env, 0.7);
    this.voices.set('chord', { oscillators: oscs, envelope: env });
  }

  private buildShimmer(midi: number): void {
    if (!this.ctx || !this.reverbBus) return;
    const env = this.ctx.createGain();
    env.gain.value = 0;
    env.connect(this.reverbBus);

    const oscs: OscillatorNode[] = [];
    const o = this.ctx.createOscillator();
    o.type = 'sine';
    o.frequency.value = midiToHz(midi + 12); // up an octave so it sparkles
    const g = this.ctx.createGain();
    g.gain.value = 0.18;
    o.connect(g).connect(env);
    o.start();
    oscs.push(o);

    this.fadeIn(env, 0.6);
    this.voices.set('shimmer', { oscillators: oscs, envelope: env });
  }

  private killVoice(key: string): void {
    const voice = this.voices.get(key);
    if (!voice || !this.ctx) return;
    const now = this.ctx.currentTime;
    voice.envelope.gain.cancelScheduledValues(now);
    voice.envelope.gain.setValueAtTime(voice.envelope.gain.value, now);
    voice.envelope.gain.linearRampToValueAtTime(0, now + FADE_OUT_SEC);
    const stopAt = now + FADE_OUT_SEC + 0.05;
    for (const o of voice.oscillators) {
      try { o.stop(stopAt); } catch { /* already stopped */ }
    }
    this.voices.delete(key);
  }

  private fadeIn(env: GainNode, target: number): void {
    if (!this.ctx) return;
    const now = this.ctx.currentTime;
    env.gain.cancelScheduledValues(now);
    env.gain.setValueAtTime(0, now);
    env.gain.linearRampToValueAtTime(target, now + FADE_IN_SEC);
  }
}
