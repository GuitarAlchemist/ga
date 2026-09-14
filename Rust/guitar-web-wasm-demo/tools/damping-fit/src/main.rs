//! Fit the guitar engine's loop-damping parameters to the per-band decay of the
//! reference recording, using `ix-acoustic-tune` (CMA-ES + per-band decay slopes).
//!
//! The engine is the same `guitar_engine` crate the WASM demo ships, linked
//! natively through its C ABI, so a fitted parameter set means the same thing in
//! the browser. Everything is seeded and deterministic.
//!
//! Usage (from this directory):
//!   cargo run --release -- eval  [decay brightness]     per-band decay error (train + held-out)
//!   cargo run --release -- fit   [--generations N] [--seed S] [--swap]
//!   cargo run --release -- sustain [decay brightness]  open-string T60 (extrapolation check)
//!   cargo run --release -- phrase <out.wav> [decay brightness]
//!
//! `phrase` renders the note sequence `scripts/record-and-analyze.js` plays, so
//! `scripts/run-spectral-critic.js` can score it (see README.md).

use guitar_engine::{
    engine_init, engine_note_on, engine_render, engine_set_brightness, engine_set_decay,
    engine_set_guitar_type, Engine,
};
use ix_acoustic_tune::cmaes::CmaEs;
use ix_acoustic_tune::reference::{default_band_edges, per_band_decay_slopes};
use ix_acoustic_tune::AskTell;
use ndarray::Array1;

const REFERENCE_WAV: &str = "../../reference/by-the-lake.wav";
const SR: f64 = 48_000.0;
/// Guitar type the demo starts on (App.jsx `useState(0)`, steel bright).
const GUITAR_TYPE: i32 = 0;
/// `[decay, brightness]` guitar type 0 shipped before this fit (the "baseline").
const BASELINE: [f64; 2] = [0.9978, 0.80];

/// One clean single-note window in the reference: onset time, pitch, and how long
/// the pitch stays stable before the next pluck overlaps it.
struct Note {
    t: f64,
    f0: f64,
    ring: f64,
}

// The ten cleanest single-note runs reported by ix's
// `cargo run -p ix-acoustic-tune --example analyze_reference -- by-the-lake.wav`
// (pass 1). Split by passage, not interleaved, so the 64-66 s phrase (three plucks
// of the same passage) cannot leak between train and held-out.
const TRAIN: [Note; 5] = [
    Note { t: 36.625, f0: 97.2, ring: 0.52 },
    Note { t: 47.400, f0: 97.4, ring: 0.52 },
    Note { t: 55.825, f0: 110.3, ring: 0.45 },
    Note { t: 60.025, f0: 98.2, ring: 0.40 },
    Note { t: 94.400, f0: 97.8, ring: 0.40 },
];
const HELD_OUT: [Note; 5] = [
    Note { t: 64.175, f0: 98.4, ring: 0.38 },
    Note { t: 64.925, f0: 98.0, ring: 0.60 },
    Note { t: 65.625, f0: 98.2, ring: 0.47 },
    Note { t: 69.850, f0: 86.0, ring: 0.50 },
    Note { t: 103.300, f0: 96.8, ring: 1.03 },
];

/// Seconds after the window start in which to look for the RMS peak, so both the
/// recording and the render are measured from the same point of the envelope.
const PEAK_SEARCH_S: f64 = 0.15;
const ENV_FRAME: usize = 256;

fn analysis_seconds(n: &Note) -> f64 {
    n.ring.clamp(0.35, 0.8)
}

/// Index of the RMS-envelope peak within the first `PEAK_SEARCH_S` of `x`.
fn peak_index(x: &[f64]) -> usize {
    let limit = ((PEAK_SEARCH_S * SR) as usize).min(x.len());
    (0..limit.saturating_sub(ENV_FRAME))
        .step_by(ENV_FRAME)
        .max_by(|&a, &b| rms(&x[a..a + ENV_FRAME]).total_cmp(&rms(&x[b..b + ENV_FRAME])))
        .unwrap_or(0)
}

fn rms(x: &[f64]) -> f64 {
    (x.iter().map(|v| v * v).sum::<f64>() / x.len().max(1) as f64).sqrt()
}

fn slopes_from_peak(x: &[f64], seconds: f64) -> Vec<f64> {
    let p = peak_index(x);
    let len = (seconds * SR) as usize;
    let seg = &x[p..(p + len).min(x.len())];
    per_band_decay_slopes(seg, SR, &default_band_edges())
}

// ---------------------------------------------------------------- parameters --

/// CMA-ES searches a unit box; this maps it onto engine parameters. `decay` is a
/// per-pass loop gain whose useful range hugs 1.0, so it is searched on a log
/// scale of `1 - decay` in [0.001, 0.010]: the low end is the demo's Decay slider
/// minimum (App.jsx `min="0.990"`), and above ~0.999 the per-string boost pushes
/// the low-E loop gain past 1.0.
const DECAY_LOG_RANGE: (f64, f64) = (-4.605_170_185_988_091, -6.907_755_278_982_137); // ln 0.010, ln 0.001

fn to_params(u: &[f64]) -> [f64; 2] {
    let (lo, hi) = DECAY_LOG_RANGE;
    let decay = 1.0 - (lo + u[0].clamp(0.0, 1.0) * (hi - lo)).exp();
    [decay, u[1].clamp(0.0, 1.0)]
}

fn to_unit(p: &[f64; 2]) -> [f64; 2] {
    let (lo, hi) = DECAY_LOG_RANGE;
    [((1.0 - p[0]).ln() - lo) / (hi - lo), p[1]]
}

fn new_engine(p: &[f64; 2]) -> *mut Engine {
    let eng = engine_init(SR as f32);
    engine_set_guitar_type(eng, GUITAR_TYPE);
    engine_set_decay(eng, p[0] as f32);
    engine_set_brightness(eng, p[1] as f32);
    eng
}

fn free_engine(eng: *mut Engine) {
    // SAFETY: `eng` came from `engine_init` (Box::into_raw) and is not used again.
    drop(unsafe { Box::from_raw(eng) });
}

fn render(eng: *mut Engine, frames: usize) -> Vec<f32> {
    let mut buf = vec![0.0f32; frames];
    engine_render(eng, buf.as_mut_ptr(), frames);
    buf
}

/// Render one pluck at the note's pitch (velocity 1.0, as the demo's buttons do).
fn render_note(p: &[f64; 2], f0: f64, seconds: f64) -> Vec<f64> {
    let eng = new_engine(p);
    engine_note_on(eng, f0 as f32, 1.0);
    let frames = (seconds * SR) as usize;
    let out = render(eng, frames);
    free_engine(eng);
    out.into_iter().map(f64::from).collect()
}

// ------------------------------------------------------------------ scoring --

struct Target {
    notes: &'static [Note],
    slopes: Vec<Vec<f64>>,
}

impl Target {
    fn new(reference: &[f64], notes: &'static [Note]) -> Self {
        let slopes = notes
            .iter()
            .map(|n| {
                let start = (n.t * SR) as usize;
                let end = start + ((PEAK_SEARCH_S + analysis_seconds(n)) * SR) as usize;
                slopes_from_peak(&reference[start..end], analysis_seconds(n))
            })
            .collect();
        Self { notes, slopes }
    }

    /// Per-band mean absolute decay-slope error (log-energy/s) over the notes.
    fn band_errors(&self, p: &[f64; 2]) -> Vec<f64> {
        let rendered: Vec<Vec<f64>> = std::thread::scope(|s| {
            let handles: Vec<_> = self
                .notes
                .iter()
                .map(|n| s.spawn(move || {
                    let secs = PEAK_SEARCH_S + analysis_seconds(n) + 0.05;
                    slopes_from_peak(&render_note(p, n.f0, secs), analysis_seconds(n))
                }))
                .collect();
            handles.into_iter().map(|h| h.join().unwrap()).collect()
        });
        let bands = self.slopes[0].len();
        (0..bands)
            .map(|b| {
                rendered
                    .iter()
                    .zip(&self.slopes)
                    .map(|(e, r)| (e[b] - r[b]).abs())
                    .sum::<f64>()
                    / self.notes.len() as f64
            })
            .collect()
    }

    fn loss(&self, p: &[f64; 2]) -> f64 {
        let e = self.band_errors(p);
        e.iter().sum::<f64>() / e.len() as f64
    }
}

fn print_table(label: &str, rows: &[(&str, Vec<f64>)]) {
    let edges = default_band_edges();
    println!("\n{label} — per-band |decay slope error| (log-energy/s, lower is better)");
    print!("  {:>14}", "band Hz");
    for (name, _) in rows {
        print!("  {name:>10}");
    }
    println!();
    for b in 0..edges.len() - 1 {
        print!("  {:>14}", format!("{:.0}-{:.0}", edges[b], edges[b + 1]));
        for (_, e) in rows {
            print!("  {:>10.3}", e[b]);
        }
        println!();
    }
    print!("  {:>14}", "mean");
    for (_, e) in rows {
        print!("  {:>10.3}", e.iter().sum::<f64>() / e.len() as f64);
    }
    println!();
}

fn print_reference(label: &str, t: &Target) {
    println!("\n{label} reference slopes (log-energy/s) per note:");
    for (n, s) in t.notes.iter().zip(&t.slopes) {
        let cells: Vec<String> = s.iter().map(|v| format!("{v:7.2}")).collect();
        println!("  t={:7.3}s f0={:5.1}Hz  {}", n.t, n.f0, cells.join(" "));
    }
}

// --------------------------------------------------------------------- main --

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let cmd = args.get(1).map(String::as_str).unwrap_or("");
    match cmd {
        "eval" => {
            let p = params_arg(&args[2..]);
            let (train, held) = targets();
            print_reference("train", &train);
            print_reference("held-out", &held);
            println!("\nparams: decay={:.5} brightness={:.4}", p[0], p[1]);
            print_table("train", &[("params", train.band_errors(&p))]);
            print_table("held-out", &[("params", held.band_errors(&p))]);
        }
        "fit" => fit(&args[2..]),
        "sustain" => {
            let p = params_arg(&args[2..]);
            println!("params: decay={:.5} brightness={:.4}", p[0], p[1]);
            for (name, f) in OPEN_STRINGS {
                println!("  {name}  {f:>6.2} Hz  T60 ~ {:5.2} s", t60_seconds(&p, f));
            }
        }
        "phrase" => {
            let out = args.get(2).expect("usage: phrase <out.wav> [decay brightness]");
            let p = params_arg(&args[3..]);
            write_wav(out, &render_phrase(&p));
            println!("wrote {out} (decay={:.5} brightness={:.4})", p[0], p[1]);
        }
        _ => {
            eprintln!("usage: guitar-damping-fit eval|fit|phrase ... (see src/main.rs)");
            std::process::exit(2);
        }
    }
}

fn params_arg(a: &[String]) -> [f64; 2] {
    match a {
        [d, b, ..] => [d.parse().expect("decay"), b.parse().expect("brightness")],
        _ => BASELINE,
    }
}

fn targets() -> (Target, Target) {
    let reference = load_wav_mono(REFERENCE_WAV);
    (Target::new(&reference, &TRAIN), Target::new(&reference, &HELD_OUT))
}

fn fit(a: &[String]) {
    let flag = |name: &str, default: usize| {
        a.iter()
            .position(|x| x == name)
            .and_then(|i| a.get(i + 1))
            .map_or(default, |v| v.parse().expect("integer flag"))
    };
    let generations = flag("--generations", 25);
    let seed = flag("--seed", 7) as u64;
    // `--swap` fits on the held-out notes and validates on the train notes (the
    // other fold of the 2-fold cross-validation).
    let (mut train, mut held) = targets();
    if a.iter().any(|x| x == "--swap") {
        std::mem::swap(&mut train, &mut held);
    }
    print_reference("train", &train);
    print_reference("held-out", &held);

    let mut opt = CmaEs::new(Array1::from(to_unit(&BASELINE).to_vec()), 0.2, seed)
        .with_bounds(Array1::zeros(2), Array1::ones(2));
    while opt.generation() < generations {
        let scored: Vec<(Array1<f64>, f64)> = opt
            .ask()
            .into_iter()
            .map(|u| {
                let loss = train.loss(&to_params(u.as_slice().unwrap()));
                (u, loss)
            })
            .collect();
        opt.tell(&scored);
        let (u, l) = opt.recommend().unwrap();
        let p = to_params(u.as_slice().unwrap());
        println!(
            "gen {:>3}  best train loss {l:.4}  decay={:.5} brightness={:.4}",
            opt.generation(),
            p[0],
            p[1]
        );
    }
    let (u, _) = opt.recommend().unwrap();
    let best = to_params(u.as_slice().unwrap());

    let (train_before, train_after) = (train.band_errors(&BASELINE), train.band_errors(&best));
    let (held_before, held_after) = (held.band_errors(&BASELINE), held.band_errors(&best));
    println!("\nbaseline: decay={:.5} brightness={:.4}", BASELINE[0], BASELINE[1]);
    println!("fitted:   decay={:.5} brightness={:.4}  (seed {seed}, {generations} generations)", best[0], best[1]);
    print_table("train", &[("baseline", train_before), ("fitted", train_after)]);
    print_table("held-out", &[("baseline", held_before.clone()), ("fitted", held_after.clone())]);
    let mean = |e: &[f64]| e.iter().sum::<f64>() / e.len() as f64;
    let verdict = if mean(&held_after) < mean(&held_before) { "IMPROVES" } else { "DOES NOT IMPROVE" };
    println!("\nheld-out verdict: fitted {verdict} on baseline ({:.3} -> {:.3})", mean(&held_before), mean(&held_after));
}

// ------------------------------------------------------------ extrapolation --

const OPEN_STRINGS: [(&str, f32); 6] =
    [("E2", 82.41), ("A2", 110.0), ("D3", 146.83), ("G3", 196.0), ("B3", 246.94), ("E4", 329.63)];

/// Broadband T60 of an open-string pluck, from the least-squares log-energy slope
/// over the 2 s after the envelope peak. The reference only has clean notes at
/// 86-110 Hz, so this is how far a fit extrapolates to the upper strings.
fn t60_seconds(p: &[f64; 2], f: f32) -> f64 {
    let x = render_note(p, f as f64, PEAK_SEARCH_S + 2.1);
    let start = peak_index(&x);
    let pts: Vec<(f64, f64)> = x[start..]
        .chunks_exact(ENV_FRAME)
        .take((2.0 * SR) as usize / ENV_FRAME)
        .enumerate()
        .map(|(i, c)| (i as f64 * ENV_FRAME as f64 / SR, (rms(c).powi(2) + 1e-20).ln()))
        .collect();
    let n = pts.len() as f64;
    let (sx, sy) = pts.iter().fold((0.0, 0.0), |(a, b), (x, y)| (a + x, b + y));
    let (sxx, sxy) = pts.iter().fold((0.0, 0.0), |(a, b), (x, y)| (a + x * x, b + x * y));
    let slope = (n * sxy - sx * sy) / (n * sxx - sx * sx);
    if slope < 0.0 { 1e6f64.ln() / -slope } else { f64::INFINITY }
}

// ------------------------------------------------------------ critic phrase --

/// The sequence `scripts/record-and-analyze.js` plays: six open strings 220 ms
/// apart, then 12-string Cmaj7 and Gmaj7 strums (App.jsx frequencies and 5 ms
/// strum stagger). The critic reads the first 4 s.
fn render_phrase(p: &[f64; 2]) -> Vec<f32> {
    let open = [82.41, 110.0, 146.83, 196.0, 246.94, 329.63];
    let cmaj7 = [(130.81, 261.63), (164.81, 329.63), (196.0, 392.0), (246.94, 493.88), (329.63, 659.26)];
    let gmaj7 = [(98.0, 196.0), (123.47, 246.94), (146.83, 293.66), (185.0, 370.0), (196.0, 392.0)];

    let mut events: Vec<(f64, f32)> = Vec::new();
    let mut t = 0.25;
    for f in open {
        events.push((t, f));
        t += 0.22;
    }
    t += 0.35;
    for (chord, next_gap) in [(&cmaj7, 1.2), (&gmaj7, 0.0)] {
        for (i, &(f, f12)) in chord.iter().enumerate() {
            let at = t + (i as f64 * 0.005).min(0.010);
            events.push((at, f as f32));
            events.push((at, f12 as f32));
        }
        t += next_gap;
    }
    events.sort_by(|a, b| a.0.total_cmp(&b.0));

    let eng = new_engine(p);
    let total = (6.0 * SR) as usize;
    let mut out = Vec::with_capacity(total);
    for (at, f) in events {
        let frame = (at * SR) as usize;
        out.extend(render(eng, frame - out.len()));
        engine_note_on(eng, f, 1.0);
    }
    out.extend(render(eng, total - out.len()));
    free_engine(eng);
    out
}

// ---------------------------------------------------------------------- wav --

fn load_wav_mono(path: &str) -> Vec<f64> {
    let bytes = std::fs::read(path).unwrap_or_else(|e| panic!("read {path}: {e}"));
    assert!(&bytes[0..4] == b"RIFF" && &bytes[8..12] == b"WAVE", "{path}: not RIFF/WAVE");
    let u16le = |o: usize| u16::from_le_bytes([bytes[o], bytes[o + 1]]);
    let u32le = |o: usize| u32::from_le_bytes([bytes[o], bytes[o + 1], bytes[o + 2], bytes[o + 3]]);
    let (mut i, mut fmt, mut data) = (12usize, None, None);
    while i + 8 <= bytes.len() {
        let len = u32le(i + 4) as usize;
        match &bytes[i..i + 4] {
            b"fmt " => fmt = Some((u16le(i + 8), u16le(i + 10), u32le(i + 12), u16le(i + 22))),
            b"data" => data = Some((i + 8, len.min(bytes.len() - i - 8))),
            _ => {}
        }
        i += 8 + len + (len & 1);
    }
    let (format, channels, sr, bits) = fmt.expect("fmt chunk");
    let (off, len) = data.expect("data chunk");
    assert!(format == 1 && bits == 16 && sr as f64 == SR, "{path}: expected 16-bit PCM at 48 kHz");
    let ch = channels.max(1) as usize;
    (0..len / (2 * ch))
        .map(|f| (0..ch).map(|c| u16le(off + (f * ch + c) * 2) as i16 as f64 / 32768.0).sum::<f64>() / ch as f64)
        .collect()
}

fn write_wav(path: &str, samples: &[f32]) {
    let data = (samples.len() * 2) as u32;
    let mut v = Vec::with_capacity(44 + data as usize);
    v.extend_from_slice(b"RIFF");
    v.extend_from_slice(&(36 + data).to_le_bytes());
    v.extend_from_slice(b"WAVEfmt ");
    v.extend_from_slice(&16u32.to_le_bytes());
    v.extend_from_slice(&1u16.to_le_bytes()); // PCM
    v.extend_from_slice(&1u16.to_le_bytes()); // mono
    v.extend_from_slice(&(SR as u32).to_le_bytes());
    v.extend_from_slice(&(SR as u32 * 2).to_le_bytes());
    v.extend_from_slice(&2u16.to_le_bytes());
    v.extend_from_slice(&16u16.to_le_bytes());
    v.extend_from_slice(b"data");
    v.extend_from_slice(&data.to_le_bytes());
    for s in samples {
        v.extend_from_slice(&((s.clamp(-1.0, 1.0) * 32767.0).round() as i16).to_le_bytes());
    }
    std::fs::write(path, v).unwrap_or_else(|e| panic!("write {path}: {e}"));
}
