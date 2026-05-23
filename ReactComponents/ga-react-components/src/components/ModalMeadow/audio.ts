/**
 * Modal Meadow — Web Audio chord-pad engine (v0.9: synth rewrite + v0.8 detune).
 *
 * v0.5–0.8 played root-position triads in a single octave (C4–C5) with two
 * oscillators per pitch and a 350ms release that overlapped the next chord's
 * 400ms attack. The result: close-interval beating in a muddy register PLUS
 * two chords stacked for ~350ms on every transition. User feedback: "Sounds
 * suck and are very dissonant."
 *
 * v0.9 keeps the raw-Web-Audio constraint (no Tone.js) but rewrites the
 * voice + envelope + transition story:
 *
 *  1. Spread voicings — each incoming triad is re-voiced across ~3 octaves:
 *     bass = root − 24, mid = original root, top = third or fifth + 12.
 *     Applied uniformly to all 7 mode progressions (Lydian, Ionian, Mixo,
 *     Dorian, Aeolian, Phrygian, Locrian) so Locrian's tritone-root chord
 *     reads atmospheric rather than crunchy.
 *  2. Per-note ADSR — attack 80ms, decay 200ms, sustain 0.6, release 900ms.
 *     Release of the OUTGOING chord starts 220ms BEFORE the new chord's
 *     attack so they don't stack into a dissonant cluster during overlap.
 *  3. Reverb via DelayNode feedback bus (≈200ms, feedback 0.4, LP-filtered,
 *     wet ≈25%). No dependency added.
 *  4. Voice timbre: sine fundamental (100%) + triangle one octave up (30%)
 *     + saw two octaves up (10%), all through a master 1.5kHz low-pass.
 *     A soft-bell + airy-harmonic ambient pad.
 *  5. Crossfade between mode buses still uses setTargetAtTime smoothing; the
 *     long release means a bus fading out doesn't click off mid-chord.
 *
 * v0.8 retained: `setDetune(centsArray)` for the descent-effect controller
 * — glides each bus's pitch down by up to ~50 cents on downhill slopes
 * and back up on uphill. Applied on top of each oscillator's intrinsic
 * detune (the triangle's -5 cent shimmer + the saw's +7 cent beating).
 *
 * The public API is unchanged: start(), setWeights(), setDetune(), setMix(),
 * dispose(), onChordPulse(), startStubPulses() — ModalMeadow.tsx still drives
 * it as in v0.8. The ChordPulseEvent shape is unchanged.
 *
 * Browser autoplay policy: callers must `start()` after a user gesture
 * (attached to canvas click + first WASD key in ModalMeadow.tsx). Before
 * that we silently do nothing.
 */

import type { ModeConfig } from './modes';

// Master output around -18 dBFS — ambient, never foreground.
const MASTER_GAIN_DB = -18;
const dbToGain = (db: number): number => Math.pow(10, db / 20);

const midiToHz = (midi: number): number => 440 * Math.pow(2, (midi - 69) / 12);

// ─── ADSR (per-note) ─────────────────────────────────────────────────────────
// Numbers tuned for a "swell" feel that lines up with 4s chords.
const ATTACK_SEC = 0.08;    // 80ms — soft, not clicky
const DECAY_SEC = 0.20;     // 200ms decay into sustain
const SUSTAIN_LEVEL = 0.6;  // 60% — leaves headroom for next chord
const RELEASE_SEC = 0.9;    // 900ms long-tail release

// Start the release of the OUTGOING chord this many seconds BEFORE the next
// chord's attack. With 4s chords + 0.9s release this means the outgoing chord
// is already 200ms into its decay tail when the new attack lands, so the two
// don't form a dissonant stack at full level.
const PRE_RELEASE_SEC = 0.22;

// Per-note headroom below master so polyphony stays under 0 dBFS.
const PER_NOTE_GAIN = dbToGain(-6);

interface ChordVoice {
  /** All running OscillatorNodes for this chord (kept so we can stop them). */
  oscillators: OscillatorNode[];
  /**
   * Each oscillator's "intrinsic" detune in cents (e.g. -5 for the triangle
   * shimmer voice, +7 for the saw). The descent-effect adds the bus-level
   * detune on top.
   */
  intrinsicDetune: number[];
  /** ADSR envelope gain. Released by `releaseVoice`. */
  envGain: GainNode;
  /** Scheduled absolute stop time, so we don't double-stop. */
  stopTime: number;
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
  /** Timeout handle for the pre-release of the current chord. */
  preReleaseTimer: number | null;
  /** Current bus-level detune in cents — applied on top of intrinsic detune. */
  detuneCents: number;
}

/**
 * Chord-pulse event fired each time a chord starts on a mode bus.
 * Consumers (the visual grass shader) read this to animate a brief
 * sway pulse synchronised with the pad attack.
 *
 *   modeIndex   — 0..N-1 (matches MODES array order: Lydian, Ionian, Mixo,
 *                 Dorian, Aeolian, Phrygian, Locrian in v0.8)
 *   chordIndex  — index into the mode's progression (0..progression.length-1)
 *   romanRoot   — degree of the chord root (1 = tonic, 4 = subdominant,
 *                 5 = dominant, 2 = supertonic, etc.). Used by the
 *                 caller to pick a directional sway pattern.
 *
 * Shape unchanged from v0.5 — visual reactivity depends on it.
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
 * a scale-degree within the mode's tonic. Each mode declares its tonic
 * pitch-class via TONIC_PC; degrees are derived from the semitone offset.
 *
 * This is good enough to drive a "tonic / subdominant / dominant" sway
 * directional cue without the caller needing a music-theory dependency.
 */
const TONIC_BY_MODE_NAME: Record<string, number> = {
  Lydian: 60,     // C
  Ionian: 60,     // C
  Mixolydian: 60, // C
  Dorian: 60,     // C
  Aeolian: 60,    // C
  Phrygian: 64,   // E
  Locrian: 59,    // B
};

const degreeOfChord = (mode: ModeConfig, chordMidi: number[]): number => {
  if (chordMidi.length === 0) return 1;
  const root = Math.min(...chordMidi);
  const tonic = TONIC_BY_MODE_NAME[mode.name] ?? 60;
  const semis = ((root - tonic) % 12 + 12) % 12;
  // Diatonic-ish lookup: 0→1, 1→4 (bII like subdominant), 2→2, 3→3 (bIII), 4→3,
  // 5→4 (IV), 6→4 (#IV / b5 — used for Locrian's tritone root), 7→5 (V),
  // 8→6 (bVI), 9→6 (VI), 10→7 (bVII), 11→7 (VII).
  const table: Record<number, number> = {
    0: 1, 1: 4, 2: 2, 3: 3, 4: 3, 5: 4, 6: 4, 7: 5, 8: 6, 9: 6, 10: 7, 11: 7,
  };
  return table[semis] ?? 1;
};

/**
 * Spread-voicing rewriter. Takes a tight root-position-ish triad and
 * re-voices it across ~3 octaves so the chord reads "full and clear",
 * not muddy or beating.
 *
 *   bass     = lowest MIDI − 24  (two octaves down)
 *   mid      = the original lowest MIDI                ("anchor")
 *   inner    = remaining chord tones from input        (kept as-is, but
 *               doubled candidates dropped to avoid stacking)
 *   top      = (chord's 3rd OR 5th, if available) + 12 (one octave above
 *               the input's top — yields the bright "9th/13th/oct" feel
 *               described in the brief without needing music-theory parsing)
 *
 * For minor chords (third = +3 from root) we keep the third in the upper
 * register where minor-thirds sound modal-elegant, not crunchy. For the
 * Phrygian bII (an F-major chord against an E-minor root) we spread the
 * half-step interval (E↔F) into a 10th by putting the F up an octave so
 * it sounds atmospheric rather than crunchy. Locrian's tritone (i°) gets
 * the same spread treatment — atmospheric across 2 octaves rather than
 * crunchy in one — implemented generically by the "+12 on the top voice".
 */
const spreadVoicing = (chord: number[]): number[] => {
  if (chord.length === 0) return chord;
  const sorted = [...chord].sort((a, b) => a - b);
  const root = sorted[0];
  const out: number[] = [];

  // Bass two octaves below the chord root → low foundation.
  out.push(root - 24);

  // Mid voice — keep the original root in its middle register.
  out.push(root);

  // Inner voices: middle chord tones, but only ones that don't duplicate the
  // bass or top register. Skip the root (already in `mid`) and the very top
  // (will be doubled an octave higher as `top`). Result for a 3-note triad
  // input: zero inner voices when triad is {root, third, fifth}, leaving
  // bass-mid-top as the three voices — exactly the brief's "3 octaves
  // between bass and top" target.
  for (let i = 1; i < sorted.length - 1; i++) {
    out.push(sorted[i]);
  }

  // Top voice: original chord's top member up one octave.
  const topInput = sorted[sorted.length - 1];
  out.push(topInput + 12);

  return out;
};

export class ModalMeadowAudio {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  /** Reverb send: input gain → delay → feedback loop → wet gain → master. */
  private reverbInput: GainNode | null = null;
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
   * Lazy-create AudioContext + reverb bus + per-mode buses. Safe to call
   * repeatedly; subsequent calls are no-ops. Must be triggered by a user
   * gesture.
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

    // ─── Reverb send (DelayNode feedback "fake reverb") ────────────────────
    // Topology:
    //   reverbInput (gain)
    //     → reverbLP (low-pass 2.5kHz) → delay (200ms)
    //         ↳ feedback (0.4) loops back into delay
    //         ↳ wetGain (25%) → master
    // Dry signal flows directly from each voice's envGain to busGain to
    // master; the reverb send is a separate parallel path.
    this.reverbInput = this.ctx.createGain();
    this.reverbInput.gain.value = 1.0;
    const reverbLP = this.ctx.createBiquadFilter();
    reverbLP.type = 'lowpass';
    reverbLP.frequency.value = 2500;
    reverbLP.Q.value = 0.5;
    const delay = this.ctx.createDelay(1.0);
    delay.delayTime.value = 0.2; // 200ms — short space, not a cathedral
    const feedback = this.ctx.createGain();
    feedback.gain.value = 0.4;
    const wetGain = this.ctx.createGain();
    wetGain.gain.value = 0.25; // 25% wet — sits behind the dry pad

    this.reverbInput.connect(reverbLP);
    reverbLP.connect(delay);
    delay.connect(feedback);
    feedback.connect(delay); // feedback loop
    delay.connect(wetGain);
    wetGain.connect(this.master);

    for (const mode of modes) {
      const busGain = this.ctx.createGain();
      busGain.gain.value = 0;             // start silent; setWeights() opens it
      busGain.connect(this.master);
      // Also feed the bus into the reverb send so the wet tail tracks
      // the per-mode mix (a faded-out bus contributes nothing to reverb).
      busGain.connect(this.reverbInput);
      const bus: ChordBus = {
        busGain,
        current: null,
        nextIndex: 0,
        timer: null,
        preReleaseTimer: null,
        detuneCents: 0,
      };
      this.buses.push(bus);
      this.advanceChord(bus, mode);
    }
  }

  /**
   * Set the relative gain of each mode bus. Weights should sum to ~1; we
   * scale by sqrt to keep perceived loudness roughly even across mixes
   * (equal-power crossfade). Smoothing avoids zipper noise.
   *
   * Accepts any length ≤ buses.length; extra entries are ignored.
   */
  setWeights(weights: number[]): void {
    if (!this.ctx) return;
    const now = this.ctx.currentTime;
    for (let i = 0; i < this.buses.length && i < weights.length; i++) {
      const target = Math.sqrt(Math.max(0, weights[i]));
      this.buses[i].busGain.gain.setTargetAtTime(target, now, 0.08);
    }
  }

  /** Backwards-compat alias for v0.5+ callers. */
  setMix(weights: number[]): void {
    this.setWeights(weights);
  }

  /**
   * Smoothly glide each bus's pitch detune (cents). Used by the descent
   * effect: descending → negative cents (pitch dips down), ascending →
   * positive cents (pitch nudges up).
   *
   * cents[i] is applied on top of each oscillator's intrinsic detune
   * (e.g. the -5 cent shimmer voice on triangle, +7 cent on saw).
   * Smoothing constant ~0.08s → reaches the target in ~0.4s, matching
   * the brief's "0.5s window".
   */
  setDetune(cents: number[]): void {
    if (!this.ctx) return;
    const now = this.ctx.currentTime;
    for (let i = 0; i < this.buses.length && i < cents.length; i++) {
      const bus = this.buses[i];
      bus.detuneCents = cents[i];
      if (bus.current) {
        for (let v = 0; v < bus.current.oscillators.length; v++) {
          const target = (bus.current.intrinsicDetune[v] ?? 0) + cents[i];
          bus.current.oscillators[v].detune.setTargetAtTime(target, now, 0.08);
        }
      }
    }
  }

  /** Stop everything and free resources. Idempotent. */
  dispose(): void {
    for (const bus of this.buses) {
      if (bus.timer !== null) {
        window.clearTimeout(bus.timer);
        bus.timer = null;
      }
      if (bus.preReleaseTimer !== null) {
        window.clearTimeout(bus.preReleaseTimer);
        bus.preReleaseTimer = null;
      }
      if (bus.current) {
        this.hardStopVoice(bus.current);
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
    this.reverbInput = null;
    this.started = false;
    this.chordPulseListeners = [];
  }

  /**
   * Stub-mode helper. When audio has not been started (no user gesture
   * yet) the caller can still drive visual pulses on a fake timer. This
   * returns a stop-callback so the caller can switch off the stub once
   * real audio kicks in. Visible-only: emits the same events.
   *
   * Unchanged from v0.5 — visual reactivity depends on this firing
   * pre-gesture so the lock-screen feels alive.
   */
  startStubPulses(modes: ModeConfig[]): () => void {
    const handles: number[] = [];
    modes.forEach((mode, modeIndex) => {
      let i = 0;
      const tick = () => {
        const chordIndex = i % mode.progression.length;
        i += 1;
        const romanRoot = degreeOfChord(mode, mode.progression[chordIndex]);
        for (const l of this.chordPulseListeners) {
          try {
            l({ modeIndex, chordIndex, romanRoot });
          } catch (err) {
            console.warn('[ModalMeadowAudio] stub-pulse listener threw', err);
          }
        }
        handles.push(window.setTimeout(tick, mode.chordDurationSec * 1000));
      };
      // Stagger the modes so all 7 stubs don't pulse on the same frame.
      handles.push(window.setTimeout(tick, 100 + modeIndex * 350));
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

    // Note: we do NOT stop the previous voice here. The previous voice's
    // release was already scheduled by the previous chord's preReleaseTimer
    // (PRE_RELEASE_SEC before now). That release ramps over RELEASE_SEC, so
    // by the time the new chord's attack peaks (~ATTACK_SEC = 80ms in), the
    // old chord is already PRE_RELEASE_SEC + ATTACK_SEC ≈ 300ms into its
    // 900ms tail — well-decayed, not stacking dissonantly.
    bus.current = this.playChord(bus, chord);

    // Emit chord-pulse for the visual sway. modeIndex = position of this bus
    // in `this.buses` (which mirrors `modes` argument order to start()).
    const modeIndex = this.buses.indexOf(bus);
    const romanRoot = degreeOfChord(mode, chord);
    for (const l of this.chordPulseListeners) {
      try {
        l({ modeIndex, chordIndex, romanRoot });
      } catch (err) {
        console.warn('[ModalMeadowAudio] chord-pulse listener threw', err);
      }
    }

    // Schedule the pre-release of THIS chord, fired (chordDuration - PRE_RELEASE_SEC)
    // from now so the outgoing release tail begins before the next chord attacks.
    const chordMs = mode.chordDurationSec * 1000;
    const preReleaseMs = Math.max(0, chordMs - PRE_RELEASE_SEC * 1000);
    const voiceToRelease = bus.current;
    bus.preReleaseTimer = window.setTimeout(() => {
      if (voiceToRelease) this.releaseVoice(voiceToRelease);
      bus.preReleaseTimer = null;
    }, preReleaseMs);

    // Schedule the next chord onset at exactly chordDurationSec.
    bus.timer = window.setTimeout(() => {
      this.advanceChord(bus, mode);
    }, chordMs);
  }

  /**
   * Spawn a single chord voice with spread voicing, layered timbre, and a
   * proper ADSR envelope. Returns the ChordVoice so the engine can release
   * it later via `releaseVoice`.
   *
   * Signal graph for one note:
   *
   *   sine(f)  ────┐
   *   tri(2f)  ──▶ mix → noteLP(1.5k) → noteGain(-6dB) → envGain(ADSR) → busGain
   *   saw(4f)  ───┘
   *
   * `envGain` is what we automate for attack/decay/release; one envGain
   * covers ALL notes in the chord so we don't lose phase relationships
   * between layers when releasing.
   *
   * Each oscillator's `detune` is set to `intrinsicDetune + bus.detuneCents`
   * so the descent-effect's `setDetune()` can slide all voices simultaneously.
   */
  private playChord(bus: ChordBus, midi: number[]): ChordVoice {
    if (!this.ctx) {
      // Returning a no-op voice keeps the type honest; never reached because
      // start() must be called before any chord plays.
      throw new Error('[ModalMeadowAudio] playChord called before start()');
    }
    const ctx = this.ctx;
    const now = ctx.currentTime;

    // Spread the input chord across ~3 octaves (see spreadVoicing for the
    // full rationale). Applied uniformly to all 7 mode progressions.
    const voiced = spreadVoicing(midi);

    // ADSR envelope shared by all notes in this chord.
    const envGain = ctx.createGain();
    envGain.gain.setValueAtTime(0.0001, now);
    // Attack: linear ramp to 1.0.
    envGain.gain.linearRampToValueAtTime(1.0, now + ATTACK_SEC);
    // Decay: setTargetAtTime to SUSTAIN_LEVEL with time-constant ≈ DECAY/3
    // (setTargetAtTime is exponential; tc ≈ duration/3 lands at ~95% of target).
    envGain.gain.setTargetAtTime(SUSTAIN_LEVEL, now + ATTACK_SEC, DECAY_SEC / 3);
    envGain.connect(bus.busGain);

    // Per-chord master low-pass (1.5kHz, Q ~0.7) — gentle slope to keep the
    // saw harmonics from biting, soft-bell character throughout.
    const noteLP = ctx.createBiquadFilter();
    noteLP.type = 'lowpass';
    noteLP.frequency.value = 1500;
    noteLP.Q.value = 0.7;
    noteLP.connect(envGain);

    // Per-chord polyphony headroom — each note hits -6dB into the LP.
    const noteGain = ctx.createGain();
    noteGain.gain.value = PER_NOTE_GAIN / Math.sqrt(voiced.length);
    noteGain.connect(noteLP);

    const oscillators: OscillatorNode[] = [];
    const intrinsicDetune: number[] = [];
    for (const m of voiced) {
      const hz = midiToHz(m);

      // Sine fundamental — the body of the voice, full level.
      const sine = ctx.createOscillator();
      sine.type = 'sine';
      sine.frequency.value = hz;
      sine.detune.value = bus.detuneCents;     // intrinsic 0 + bus detune
      const sineMix = ctx.createGain();
      sineMix.gain.value = 1.0;
      sine.connect(sineMix);
      sineMix.connect(noteGain);
      sine.start(now);
      oscillators.push(sine);
      intrinsicDetune.push(0);

      // Triangle one octave up at 30% — adds gentle upper-body warmth.
      const tri = ctx.createOscillator();
      tri.type = 'triangle';
      tri.frequency.value = hz * 2;
      tri.detune.value = -5 + bus.detuneCents; // slight chorus shimmer
      const triMix = ctx.createGain();
      triMix.gain.value = 0.30;
      tri.connect(triMix);
      triMix.connect(noteGain);
      tri.start(now);
      oscillators.push(tri);
      intrinsicDetune.push(-5);

      // Saw two octaves up at 10% — air/harmonics. The LP hard-rolls
      // most of this above 1.5kHz; what's left adds presence without bite.
      const saw = ctx.createOscillator();
      saw.type = 'sawtooth';
      saw.frequency.value = hz * 4;
      saw.detune.value = 7 + bus.detuneCents;  // 7 cents sharp → tiny beating
      const sawMix = ctx.createGain();
      sawMix.gain.value = 0.10;
      saw.connect(sawMix);
      sawMix.connect(noteGain);
      saw.start(now);
      oscillators.push(saw);
      intrinsicDetune.push(7);
    }

    return {
      oscillators,
      intrinsicDetune,
      envGain,
      // stopTime is set when releaseVoice runs; initialise to a sentinel.
      stopTime: 0,
    };
  }

  /**
   * Begin the release tail of a voice. Linear ramp to 0 over RELEASE_SEC.
   * Oscillators are scheduled to stop at the end of the ramp so they free
   * automatically. Safe to call multiple times — the second call is a no-op
   * because the oscillators are already scheduled to stop.
   */
  private releaseVoice(voice: ChordVoice): void {
    if (!this.ctx) return;
    if (voice.stopTime !== 0) return; // already released
    const now = this.ctx.currentTime;
    const env = voice.envGain.gain;
    // Cancel any pending decay-target ramps and pin to the current value so
    // the release starts from where we are, not from SUSTAIN_LEVEL.
    env.cancelScheduledValues(now);
    env.setValueAtTime(env.value, now);
    env.linearRampToValueAtTime(0, now + RELEASE_SEC);
    voice.stopTime = now + RELEASE_SEC + 0.05;
    for (const osc of voice.oscillators) {
      try {
        osc.stop(voice.stopTime);
      } catch {
        // Older Safari throws InvalidStateError if stop() called twice;
        // we never do, but the guard is cheap.
      }
    }
  }

  /**
   * Synchronous hard-stop used by dispose(). Cuts the env to zero
   * immediately and stops oscillators at now — only acceptable on unmount.
   */
  private hardStopVoice(voice: ChordVoice): void {
    if (!this.ctx) return;
    const now = this.ctx.currentTime;
    voice.envGain.gain.cancelScheduledValues(now);
    voice.envGain.gain.setValueAtTime(0, now);
    for (const osc of voice.oscillators) {
      try {
        osc.stop(now);
      } catch {
        // Already stopped — fine.
      }
    }
  }
}
