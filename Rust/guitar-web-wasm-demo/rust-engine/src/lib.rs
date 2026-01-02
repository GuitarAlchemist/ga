// GA Guitar WASM demo – iteration (2025-11-16T23:06:19-05:00, dbc4548, Automated acoustic iteration)
use core::f32::consts::TAU;

/// Simple linear congruential generator for deterministic noise.
/// Returns a value in [-1, 1].
#[inline]
fn lcg_next(seed: &mut u32) -> f32 {
    // Values from Numerical Recipes
    *seed = seed.wrapping_mul(1664525).wrapping_add(1013904223);
    // Use upper bits and map to [-1, 1]
    let v = ((*seed >> 8) as f32) * (1.0 / ((u32::MAX >> 8) as f32));
    v * 2.0 - 1.0
}

struct Resonator {
    a1: f32,
    a2: f32,
    y1: f32,
    y2: f32,
    gain: f32,
}

impl Resonator {
    fn new(sample_rate: f32, freq_hz: f32, r: f32, gain: f32) -> Self {
        let sr = sample_rate.max(1.0);
        let omega = TAU * freq_hz / sr;
        let a1 = 2.0 * r * omega.cos();
        let a2 = -r * r;
        Self {
            a1,
            a2,
            y1: 0.0,
            y2: 0.0,
            gain,
        }
    }

    #[inline]
    fn process(&mut self, x: f32) -> f32 {
        let y = x * self.gain + self.a1 * self.y1 + self.a2 * self.y2;
        self.y2 = self.y1;
        self.y1 = y;
        y
    }
}

const MAX_VOICES: usize = 8;

/// Per-string voice (polyphonic Karplus–Strong)
struct Voice {
    buffer: std::vec::Vec<f32>,
    buffer_idx: usize,
    buffer_len: usize,
    frac_delay: f32,
    level: f32,
    freq_hz: f32,

    lp_state: f32,
    ap_x1: f32,
    ap_y1: f32,
    hp_state: f32,
    bridge_state: f32,

    attack_level: f32,
    sustain: f32,
    pluck_offset: usize,
    pluck_mix: f32,
    active: bool,
}

impl Voice {
    fn new() -> Self {
        Self {
            buffer: Vec::new(),
            buffer_idx: 0,
            buffer_len: 0,
            frac_delay: 0.0,
            level: 0.0,
            freq_hz: 110.0,
            lp_state: 0.0,
            ap_x1: 0.0,
            ap_y1: 0.0,
            hp_state: 0.0,
            bridge_state: 0.0,
            attack_level: 0.0,
            sustain: 0.995,
            pluck_offset: 1,
            pluck_mix: 0.3,
            active: false,
        }
    }
}

#[repr(C)]
pub struct Engine {
    sample_rate: f32,
    decay: f32,
    noise_seed: u32,
    brightness: f32,
    dispersion: f32,
    attack_decay: f32,
    guitar_type: i32,
    // Polyphonic voices (multiple strings)
    voices: std::vec::Vec<Voice>,
    // Simple convolution reverb
    reverb_ir: std::vec::Vec<f32>,
    reverb_buf: std::vec::Vec<f32>,
    reverb_pos: usize,
    reverb_mix: f32,
    // Simple body resonators
    resonators: std::vec::Vec<Resonator>,
}

impl Engine {
    fn new(sample_rate: f32) -> Self {
        let sr = sample_rate.max(1.0);

        // --- Body resonators: air + top + soundboard + bridge + brilliance ---
        let mut resonators = std::vec::Vec::with_capacity(5);
        // air cavity
        resonators.push(Resonator::new(sr, 110.0, 0.95, 0.08));
        // top plate (warmth)
        resonators.push(Resonator::new(sr, 240.0, 0.96, 0.10));
        // soundboard main resonance
        resonators.push(Resonator::new(sr, 530.0, 0.97, 0.08));
        // bridge / fretboard coupling
        resonators.push(Resonator::new(sr, 1200.0, 0.98, 0.03));
        // brilliance peak
        resonators.push(Resonator::new(sr, 2400.0, 0.92, 0.02));

        // --- Small synthetic impulse response for convolution reverb ---
        // Shorter, room-like tail (~0.10 s) with gentle HF damping
        let mut ir_len = (sr * 0.10) as usize; // ~100 ms
        if ir_len < 64 {
            ir_len = 64;
        } else if ir_len > 8192 {
            ir_len = 8192;
        }
        let mut reverb_ir = vec![0.0; ir_len];
        let mut seed = 1234567u32;
        {
            let tail_s = ir_len as f32 / sr;
            let mut prev = 0.0f32;
            for (n, v) in reverb_ir.iter_mut().enumerate() {
                let t = n as f32 / sr;
                // Exponential amplitude envelope for the tail
                let env = (-t / 0.20).exp();
                // Increase HF damping deeper into the tail by increasing smoothing
                let hf = (t / tail_s).clamp(0.0, 1.0);
                let alpha = (0.25 + 0.55 * hf).clamp(0.0, 0.98); // stronger smoothing later
                let noise = lcg_next(&mut seed);
                let filtered = alpha * prev + (1.0 - alpha) * noise;
                prev = filtered;
                *v = env * filtered * 0.06; // slightly higher base, renormalized below
            }
        }
        // Early reflections cluster: body/small room cues (times in seconds)
        let taps: &[(f32, f32)] = &[
            (0.0025, 0.75), (0.0049, 0.55), (0.0073, 0.40), (0.0110, 0.28),
            (0.0170, 0.22), (0.0260, 0.18), (0.0350, 0.14), (0.0480, 0.12),
        ];
        for (t_s, a) in taps.iter().copied() {
            let idx = (t_s * sr) as usize;
            if idx < reverb_ir.len() {
                reverb_ir[idx] += a;
            }
        }
        // Rough normalization (keeps relative shape, tames overall gain)
        let norm = reverb_ir.iter().map(|v| v.abs()).sum::<f32>().max(1.0);
        for v in &mut reverb_ir {
            *v /= norm;
        }
        let reverb_buf = vec![0.0; ir_len];

        // --- Voices ---
        let mut voices = std::vec::Vec::with_capacity(MAX_VOICES);
        for _ in 0..MAX_VOICES {
            voices.push(Voice::new());
        }

        let mut engine = Self {
            sample_rate,
            decay: 0.9968,
            noise_seed: 1,
            brightness: 0.70,
            dispersion: 0.20,
            attack_decay: 0.988,
            guitar_type: 0,
            voices,
            reverb_ir,
            reverb_buf,
            reverb_pos: 0,
            reverb_mix: 0.10,
            resonators,
        };
        engine.set_guitar_profile(0);
        engine
    }
    fn set_guitar_profile(&mut self, guitar_type: i32) {
        let t = match guitar_type {
            0 | 1 | 2 | 3 => guitar_type,
            _ => 0,
        };
        self.guitar_type = t;

        match t {
            // 0 – Steel bright: tight, bright, slightly shorter reverb
            0 => {
                self.decay = 0.9978;
                self.brightness = 0.80;
                self.dispersion = 0.22;
                self.attack_decay = 0.986;
                self.reverb_mix = 0.14;
            }
            // 1 – Steel warm: rounder, a bit darker, slightly longer sustain
            1 => {
                self.decay = 0.9982;
                self.brightness = 0.60;
                self.dispersion = 0.16;
                self.attack_decay = 0.987;
                self.reverb_mix = 0.16;
            }
            // 2 – Nylon: softer transients, darker highs, long sustain
            2 => {
                self.decay = 0.9985;
                self.brightness = 0.45;
                self.dispersion = 0.07;
                self.attack_decay = 0.989;
                self.reverb_mix = 0.12;
            }
            // 3 – Jumbo steel: big body, brighter top, more reverb
            3 => {
                self.decay = 0.9987;
                self.brightness = 0.70;
                self.dispersion = 0.20;
                self.attack_decay = 0.985;
                self.reverb_mix = 0.18;
            }
            _ => {}
        }

        // Safety clamps (in case parameters are hand-tweaked later)
        self.decay = self.decay.clamp(0.95, 0.9999);
        self.brightness = self.brightness.clamp(0.0, 1.0);
        self.dispersion = self.dispersion.clamp(0.0, 0.5);
        self.attack_decay = self.attack_decay.clamp(0.95, 0.999);
        self.reverb_mix = self.reverb_mix.clamp(0.0, 0.9);
    }


    #[inline]
    fn process_reverb(&mut self, x: f32) -> f32 {
        if self.reverb_ir.is_empty() || self.reverb_buf.is_empty() {
            return x;
        }
        let len = self.reverb_ir.len();
        if len != self.reverb_buf.len() {
            return x;
        }

        // Write into circular buffer
        self.reverb_buf[self.reverb_pos] = x;

        // Time-domain convolution (short IR → OK)
        let mut acc = 0.0;
        let mut idx = self.reverb_pos;
        for k in 0..len {
            acc += self.reverb_ir[k] * self.reverb_buf[idx];
            idx = if idx == 0 { len - 1 } else { idx - 1 };
        }

        // Advance circular index
        self.reverb_pos += 1;
        if self.reverb_pos >= len {
            self.reverb_pos = 0;
        }

        acc
    }

    fn excite(&mut self, freq: f32, velocity: f32) {
        let sr = self.sample_rate.max(1.0);
        let f = freq.max(20.0);
        let vel = velocity.clamp(0.0, 1.0);

        // Delay-line length for Karplus–Strong
        let mut length = (sr / f).floor() as usize;
        if length < 2 {
            length = 2;
        }

        // Choose a voice: first inactive, otherwise the quietest one
        let mut target_index: Option<usize> = None;
        for (i, v) in self.voices.iter().enumerate() {
            if !v.active {
                target_index = Some(i);
                break;
            }
        }
        if target_index.is_none() {
            let mut best_i = 0usize;
            let mut best_level = f32::MAX;
            for (i, v) in self.voices.iter().enumerate() {
                if v.level < best_level {
                    best_level = v.level;
                    best_i = i;
                }
            }
            target_index = Some(best_i);
        }

        let voice = &mut self.voices[target_index.unwrap()];

        if voice.buffer.len() < length {
            voice.buffer.resize(length, 0.0);
        }
        voice.buffer_len = length;
        voice.buffer_idx = 0;

        // fractional delay to fine-tune frequency
        let exact_len = sr / f;
        let int_len = length as f32;
        voice.frac_delay = (exact_len - int_len).clamp(0.0, 0.999);

        // --- Profil en fréquence par corde + reset des états ---
        voice.freq_hz = f;
        // 82 Hz (E2) → 0.0, 330 Hz (E4) → 1.0
        let f_clamped = f.clamp(82.0, 330.0);
        let f_norm = ((f_clamped - 82.0) / (330.0 - 82.0)).clamp(0.0, 1.0);

        // Reset filtres / états
        voice.lp_state = 0.0;
        voice.ap_x1 = 0.0;
        voice.ap_y1 = 0.0;
        voice.hp_state = 0.0;
        voice.bridge_state = 0.0;

        // Sustain légèrement plus long dans les graves
        voice.sustain = (0.995 + 0.006 * (1.0 - f_norm)).min(0.9998);

        // Pick position heuristics: higher strings → plus proche du chevalet
        let mut pick_ratio = (0.18 + 0.60 * (1.0 - vel)).clamp(0.1, 0.85);
        pick_ratio = pick_ratio.min(0.95);
        let mut pick_offset = ((length as f32) * pick_ratio).round() as usize;
        let max_offset = length.saturating_sub(1).max(1);
        pick_offset = pick_offset.clamp(1, max_offset);
        voice.pluck_offset = pick_offset;
        voice.pluck_mix = (0.15 + 0.35 * vel).clamp(0.05, 0.7);

        // --- Excitation : bruit band-limité + shape selon pluck_offset ---
        let pick_pos = voice.pluck_offset as f32 / (length as f32);
        let mut prev = 0.0f32;
        for (i, s) in voice.buffer.iter_mut().enumerate() {
            let n = lcg_next(&mut self.noise_seed);
            // petit low-pass sur le bruit
            let filtered = 0.5 * n + 0.5 * prev;
            prev = filtered;

            // profil de pincement : max d’énergie autour du point de pincement
            let pos = i as f32 / (length as f32);
            let dist = (pos - pick_pos).abs();
            let pick_shape = (1.0 - dist * 2.5).clamp(0.0, 1.0);

            *s = filtered * vel * pick_shape;
        }

        // Smooth le tout début pour éviter les clicks
        if length > 4 {
            let mut prev = voice.buffer[0];
            for i in 1..4 {
                let cur = voice.buffer[i];
                voice.buffer[i] = 0.5 * cur + 0.5 * prev;
                prev = cur;
            }
        }

        // Attack : moins bruitée, légèrement plus forte dans les graves
        let attack_scale = 0.03 + 0.03 * (1.0 - f_norm);
        voice.attack_level = vel * attack_scale;

        voice.level = vel.abs();
        voice.active = true;
    }

    fn render(&mut self, out: &mut [f32]) {
        if self.sample_rate <= 0.0 {
            out.fill(0.0);
            return;
        }

        let base_decay = self.decay;
        let lp_alpha = 0.05;
        let base_brightness = self.brightness;
        let base_dispersion = self.dispersion;
        let base_attack_decay = self.attack_decay;
        let mix = self.reverb_mix;

        for s in out.iter_mut() {
            let mut string_sum: f32 = 0.0;
            let mut active_count: f32 = 0.0;

            for voice in &mut self.voices {
                if !voice.active || voice.buffer_len < 2 {
                    continue;
                }

                active_count += 1.0;

                let len = voice.buffer_len;
                let i = voice.buffer_idx;
                let j = (i + 1) % len;

                // --- Frequency-dependent profile per voice ---
                let f = voice.freq_hz.max(20.0);
                let f_norm = ((f - 82.0) / (330.0 - 82.0)).clamp(0.0, 1.0);

                // Folk bright profile per voice
                let decay = (base_decay + 0.0025 * (1.0 - f_norm)) * voice.sustain;
                let brightness = (base_brightness + 0.45 * f_norm + voice.pluck_mix * 0.30)
                    .clamp(0.0, 1.0);
                let dispersion =
                    (base_dispersion * (0.15 + 0.25 * f_norm)).clamp(0.0, 0.35);
                let attack_decay =
                    (base_attack_decay - 0.004 * f_norm).clamp(0.97, 0.993);

                let curr = voice.buffer[i];
                let next = voice.buffer[j];
                let interp = curr + (next - curr) * voice.frac_delay;

                // moyenne classique KS
                let avg = 0.5 * (interp + curr);

                // --- All-pass pour dispersion (inharmonicité légère) ---
                let a = dispersion;
                let ap = -a * avg + voice.ap_x1 + a * voice.ap_y1;
                voice.ap_x1 = avg;
                voice.ap_y1 = ap;

                // --- Low-pass dans le feedback ---
                voice.lp_state += lp_alpha * (ap - voice.lp_state);

                // --- Brightness : mix entre chemin brillant et chemin filtré ---
                let y = decay * (brightness * ap + (1.0 - brightness) * voice.lp_state);

                // feedback dans le buffer
                voice.buffer[voice.buffer_idx] = y;
                voice.buffer_idx = j;

                voice.level = 0.997 * voice.level + 0.003 * y.abs();

                let mut sample = y;

                if voice.attack_level > 1.0e-3 {
                    let attack_noise = lcg_next(&mut self.noise_seed);
                    // high-pass plus shaping: bright pick, mais courte
                    let hp_noise = attack_noise - voice.lp_state * 0.8;
                    let attack_shape = 0.45 + 0.35 * f_norm;
                    sample += hp_noise * voice.attack_level * attack_shape;
                    voice.attack_level *= attack_decay;
                }

                if voice.level < 5.0e-6 && voice.attack_level < 5.0e-5 {
                    voice.active = false;
                }

                string_sum += sample;
            }

            // normalisation polyphonique
            let norm = active_count.max(1.0);
            string_sum /= norm;

            // Pass through simple body resonators
            let mut body = 0.0;
            for r in &mut self.resonators {
                body += r.process(string_sum);
            }

            // Mix dry string and body response, then reverb
            let dry = 0.55 * string_sum + 0.45 * body;
            let wet = self.process_reverb(dry);
            let mut out_sample = dry * (1.0 - mix) + wet * mix;

            // Very simple soft clip safeguard
            let limit = 0.95;
            if out_sample > limit {
                out_sample = limit + (out_sample - limit) * 0.2;
            } else if out_sample < -limit {
                out_sample = -limit + (out_sample + limit) * 0.2;
            }

            *s = out_sample;
        }
    }
}

#[no_mangle]
pub extern "C" fn engine_set_guitar_type(engine: *mut Engine, guitar_type: i32) {
    if engine.is_null() {
        return;
    }
    let engine = unsafe { &mut *engine };
    engine.set_guitar_profile(guitar_type);
}

#[no_mangle]
pub extern "C" fn engine_init(sample_rate: f32) -> *mut Engine {
    let engine = Engine::new(sample_rate);
    Box::into_raw(Box::new(engine))
}

#[no_mangle]
pub extern "C" fn engine_note_on(engine: *mut Engine, freq: f32, velocity: f32) {
    if engine.is_null() {
        return;
    }
    let engine = unsafe { &mut *engine };
    engine.excite(freq, velocity);
}

#[no_mangle]
pub extern "C" fn engine_set_decay(engine: *mut Engine, decay: f32) {
    if engine.is_null() {
        return;
    }
    let engine = unsafe { &mut *engine };
    // Keep decay in a sensible range for Karplus–Strong
    engine.decay = decay.clamp(0.95, 0.9999);
}

#[no_mangle]
pub extern "C" fn engine_set_brightness(engine: *mut Engine, brightness: f32) {
    if engine.is_null() {
        return;
    }
    let engine = unsafe { &mut *engine };
    engine.brightness = brightness.clamp(0.0, 1.0);
}

#[no_mangle]
pub extern "C" fn engine_set_dispersion(engine: *mut Engine, dispersion: f32) {
    if engine.is_null() {
        return;
    }
    let engine = unsafe { &mut *engine };
    // Keep dispersion subtle to avoid toy-ish artifacts
    engine.dispersion = dispersion.clamp(0.0, 0.5);
}

#[no_mangle]
pub extern "C" fn engine_set_attack_decay(engine: *mut Engine, attack_decay: f32) {
    if engine.is_null() {
        return;
    }
    let engine = unsafe { &mut *engine };
    engine.attack_decay = attack_decay.clamp(0.95, 0.999);
}

#[no_mangle]
pub extern "C" fn engine_set_reverb_mix(engine: *mut Engine, mix: f32) {
    if engine.is_null() {
        return;
    }
    let engine = unsafe { &mut *engine };
    engine.reverb_mix = mix.clamp(0.0, 0.9);
}

#[no_mangle]
pub extern "C" fn engine_render(engine: *mut Engine, buffer: *mut f32, frames: usize) {
    if engine.is_null() || buffer.is_null() || frames == 0 {
        return;
    }
    let engine = unsafe { &mut *engine };
    let out = unsafe { std::slice::from_raw_parts_mut(buffer, frames) };
    engine.render(out);
}

#[no_mangle]
pub extern "C" fn alloc_buffer(frames: usize) -> *mut f32 {
    if frames == 0 {
        return std::ptr::null_mut();
    }
    let mut buf: Vec<f32> = vec![0.0; frames];
    let ptr = buf.as_mut_ptr();
    std::mem::forget(buf); // leak on purpose for WASM lifetime
    ptr
}
