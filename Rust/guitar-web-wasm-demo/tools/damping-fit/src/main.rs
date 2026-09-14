//! Fit the guitar engine's loop-damping parameters to the per-band decay of the
//! reference recording, using `ix-acoustic-tune` (CMA-ES + band decay slopes).
//!
//! The engine source is compiled straight into this binary (`#[path]` module
//! below), so a fitted parameter set means the same thing in the browser without
//! changing how the shipped WASM is built. Everything is seeded and deterministic.
//!
//! Usage (from this directory):
//!   cargo run --release -- eval    [decay brightness]  per-band decay error (train + held-out)
//!   cargo run --release -- grid    [brightness]        decay sweep on both folds and their union
//!   cargo run --release -- fit     [--generations N] [--seed S] [--swap]
//!   cargo run --release -- sustain [decay brightness]  open-string T60 (extrapolation check)
//!   cargo run --release -- phrase  <out.wav> [decay brightness]
//!
//! `phrase` renders the note sequence `scripts/record-and-analyze.js` plays, so
//! `scripts/run-spectral-critic.js` can score it (see README.md).

#[allow(dead_code, clippy::all)]
#[path = "../../../rust-engine/src/lib.rs"]
mod engine;

use engine::{
    engine_init, engine_note_on, engine_render, engine_set_brightness, engine_set_decay,
    engine_set_guitar_type, Engine,
};
use ix_acoustic_tune::cmaes::CmaEs;
use ix_acoustic_tune::reference::{band_decay_slope, default_band_edges, DEFAULT_HOP, DEFAULT_WINDOW};
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
// of the same passage) cannot leak between train and held-out. 8 of the 10 are the
// same ~97 Hz pitch: this validates re-plucks of one register, not the neck.
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

/// A reference band is only fitted while its energy stays this far above the
/// recording's own noise floor in that band (Lundeby-style truncation).
const FLOOR_MARGIN_DB: f64 = 10.0;
/// Percentile of whole-recording frame energy taken as a band's noise floor.
const FLOOR_PERCENTILE: f64 = 0.05;
/// A (note, band) pair needs at least this much time above the floor...
const MIN_FIT_SECONDS: f64 = 0.10;
/// ...and must fall by at least this much over it, else it is dropped.
const MIN_DROP_DB: f64 = 3.0;
/// A band only counts toward the loss if this many notes survive in it.
const MIN_NOTES_PER_BAND: usize = 2;

/// Early decay only: the reference rings 0.38-1.03 s before the next pluck, so
/// slopes describe the first ~0.8 s, not a multi-second T60.
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

// ------------------------------------------------------------- noise floor --

/// Per-frame energy in each band of `band_edges`, with the same STFT and bin
/// mapping as `ix_acoustic_tune::reference::band_decay_slope`.
fn band_energy_track(x: &[f64], hop: usize) -> Vec<Vec<f64>> {
    let edges = default_band_edges();
    let spec = ix_signal::spectral::spectrogram(x, DEFAULT_WINDOW, hop, false);
    let bin_hz = SR / DEFAULT_WINDOW as f64;
    let n_bins = DEFAULT_WINDOW / 2 + 1;
    let ranges: Vec<(usize, usize)> = edges
        .windows(2)
        .map(|w| {
            let lo = ((w[0] / bin_hz).floor() as usize).min(n_bins - 1);
            let hi = ((w[1] / bin_hz).ceil() as usize).min(n_bins - 1);
            (lo, hi)
        })
        .collect();
    spec.iter()
        .map(|frame| ranges.iter().map(|&(lo, hi)| (lo..=hi).map(|k| frame[k] * frame[k]).sum()).collect())
        .collect()
}

/// Noise floor of each band: a low percentile of frame energy over the whole
/// recording (a real performance has no silence; a render's floor is zero).
fn band_noise_floors(recording: &[f64]) -> Vec<f64> {
    let track = band_energy_track(recording, DEFAULT_WINDOW);
    (0..track[0].len())
        .map(|b| {
            let mut e: Vec<f64> = track.iter().map(|f| f[b]).collect();
            e.sort_by(f64::total_cmp);
            e[((e.len() - 1) as f64 * FLOOR_PERCENTILE) as usize]
        })
        .collect()
}

/// Number of STFT frames from the start of `seg` before band `b` first drops to
/// `floor + FLOOR_MARGIN_DB`. The fit uses only those frames.
fn frames_above_floor(track: &[Vec<f64>], b: usize, floor: f64) -> usize {
    let threshold = floor * 10f64.powf(FLOOR_MARGIN_DB / 10.0);
    track.iter().position(|f| f[b] <= threshold).unwrap_or(track.len())
}

/// Samples spanning exactly `frames` STFT frames.
fn samples_for_frames(frames: usize) -> usize {
    (frames.max(1) - 1) * DEFAULT_HOP + DEFAULT_WINDOW
}

/// Where a reference (note, band) slope is measured: `Some(sample span)` from the
/// envelope peak if it clears the floor long enough and actually decays.
struct BandFit {
    slope: f64,
    span: usize,
}

fn fit_band(seg: &[f64], track: &[Vec<f64>], b: usize, floor: f64) -> Option<BandFit> {
    let frames = frames_above_floor(track, b, floor);
    let span = samples_for_frames(frames).min(seg.len());
    if (span as f64) < MIN_FIT_SECONDS * SR + DEFAULT_WINDOW as f64 {
        return None;
    }
    let edges = default_band_edges();
    let slope = band_decay_slope(&seg[..span], SR, edges[b], edges[b + 1], DEFAULT_WINDOW, DEFAULT_HOP);
    let seconds = (frames.max(1) - 1) as f64 * DEFAULT_HOP as f64 / SR;
    let drop_db = -slope * seconds * 10.0 / std::f64::consts::LN_10;
    (slope < 0.0 && drop_db >= MIN_DROP_DB).then_some(BandFit { slope, span })
}

// -------------------------------------------------------------- stability --

/// Reject any decay whose loop gain bound reaches `1 - STABILITY_EPS`.
const STABILITY_EPS: f32 = 1e-4;

/// Upper bound of the engine's per-voice loop gain at pitch `f`, mirroring
/// `render()`: `(decay + 0.0025·(1-f_norm))·sustain`. The KS average, dispersion
/// allpass and LP/bright mix all have |H| <= 1, so this bounds the whole loop.
fn loop_gain_bound(decay: f32, f: f32) -> f32 {
    let f_norm = ((f.clamp(82.0, 330.0) - 82.0) / (330.0 - 82.0)).clamp(0.0, 1.0);
    let sustain = (0.995 + 0.006 * (1.0 - f_norm)).min(0.9998);
    (decay.clamp(0.95, 0.9999) + 0.0025 * (1.0 - f_norm)) * sustain
}

/// Stable for every pitch the demo can play (E2 to the 12-string's high E, 82-1319 Hz).
fn is_stable(decay: f64) -> bool {
    (82..=1319).all(|f| loop_gain_bound(decay as f32, f as f32) < 1.0 - STABILITY_EPS)
}

// ---------------------------------------------------------------- parameters --

/// CMA-ES searches a unit box; this maps it onto engine parameters. `decay` is a
/// per-pass loop gain whose useful range hugs 1.0, so it is searched on a log
/// scale of `1 - decay` in [0.0002, 0.03]. The top of that range is unstable on
/// the low strings; `Target::loss` rejects it via `is_stable`.
const DECAY_LOG_RANGE: (f64, f64) = (-3.506_557_897_319_982, -8.517_193_191_416_238); // ln 0.03, ln 0.0002

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
    /// `fits[note][band]`: the reference slope and the span it was measured over,
    /// or `None` if that band is too close to the noise floor for that note.
    fits: Vec<Vec<Option<BandFit>>>,
}

fn peak_segment<'a>(x: &'a [f64], n: &Note) -> &'a [f64] {
    let p = peak_index(x);
    &x[p..(p + (analysis_seconds(n) * SR) as usize).min(x.len())]
}

impl Target {
    fn new(reference: &[f64], floors: &[f64], notes: &'static [Note]) -> Self {
        let fits = notes
            .iter()
            .map(|n| {
                let start = (n.t * SR) as usize;
                let end = start + ((PEAK_SEARCH_S + analysis_seconds(n)) * SR) as usize;
                let seg = peak_segment(&reference[start..end], n);
                let track = band_energy_track(seg, DEFAULT_HOP);
                (0..floors.len()).map(|b| fit_band(seg, &track, b, floors[b])).collect()
            })
            .collect();
        Self { notes, fits }
    }

    fn bands(&self) -> usize {
        self.fits[0].len()
    }

    fn survivors(&self, b: usize) -> usize {
        self.fits.iter().filter(|f| f[b].is_some()).count()
    }

    /// Render slopes `[note][band]`, measured over the same span as the reference
    /// slope for that (note, band); `None` where the reference band was dropped.
    fn rendered_slopes(&self, p: &[f64; 2]) -> Vec<Vec<Option<f64>>> {
        let edges = default_band_edges();
        std::thread::scope(|s| {
            let handles: Vec<_> = self
                .notes
                .iter()
                .zip(&self.fits)
                .map(|(n, fits)| {
                    let edges = &edges;
                    s.spawn(move || {
                        let secs = PEAK_SEARCH_S + analysis_seconds(n) + 0.05;
                        let x = render_note(p, n.f0, secs);
                        let seg = peak_segment(&x, n);
                        fits.iter()
                            .enumerate()
                            .map(|(b, fit)| {
                                let span = fit.as_ref()?.span.min(seg.len());
                                Some(band_decay_slope(&seg[..span], SR, edges[b], edges[b + 1], DEFAULT_WINDOW, DEFAULT_HOP))
                            })
                            .collect()
                    })
                })
                .collect();
            handles.into_iter().map(|h| h.join().unwrap()).collect()
        })
    }

    /// Per-band median |decay slope error| (log-energy/s) over the notes that
    /// survive the floor. `None` for bands with fewer than `MIN_NOTES_PER_BAND`.
    fn band_errors(&self, p: &[f64; 2]) -> Vec<Option<f64>> {
        let rendered = self.rendered_slopes(p);
        (0..self.bands())
            .map(|b| {
                let mut errs: Vec<f64> = rendered
                    .iter()
                    .zip(&self.fits)
                    .filter_map(|(r, fits)| Some((r[b]? - fits[b].as_ref()?.slope).abs()))
                    .collect();
                (errs.len() >= MIN_NOTES_PER_BAND).then(|| median(&mut errs))
            })
            .collect()
    }

    /// Mean over scored bands; unstable parameters are rejected outright.
    fn loss(&self, p: &[f64; 2]) -> f64 {
        if !is_stable(p[0]) {
            return REJECTED;
        }
        mean_scored(&self.band_errors(p)).min(REJECTED)
    }
}

/// Loss for parameters that fail the stability constraint.
const REJECTED: f64 = 1e3;

fn median(v: &mut [f64]) -> f64 {
    v.sort_by(f64::total_cmp);
    let m = v.len() / 2;
    if v.len().is_multiple_of(2) { 0.5 * (v[m - 1] + v[m]) } else { v[m] }
}

/// Mean over the bands that were scored; infinite if the floor dropped them all.
fn mean_scored(e: &[Option<f64>]) -> f64 {
    let s: Vec<f64> = e.iter().flatten().copied().collect();
    if s.is_empty() { f64::INFINITY } else { s.iter().sum::<f64>() / s.len() as f64 }
}

fn print_table(label: &str, target: &Target, rows: &[(&str, Vec<Option<f64>>)]) {
    let edges = default_band_edges();
    println!("\n{label} — per-band median |decay slope error| (log-energy/s, lower is better)");
    print!("  {:>14}  {:>5}", "band Hz", "notes");
    for (name, _) in rows {
        print!("  {name:>10}");
    }
    println!();
    for b in 0..edges.len() - 1 {
        print!("  {:>14}  {:>5}", format!("{:.0}-{:.0}", edges[b], edges[b + 1]), target.survivors(b));
        for (_, e) in rows {
            match e[b] {
                Some(v) => print!("  {v:>10.3}"),
                None => print!("  {:>10}", "dropped"),
            }
        }
        println!();
    }
    print!("  {:>14}  {:>5}", "mean", "");
    for (_, e) in rows {
        print!("  {:>10.3}", mean_scored(e));
    }
    println!();
}

fn print_reference(label: &str, t: &Target) {
    println!("\n{label} reference slopes above the noise floor (log-energy/s; '-' = dropped):");
    for (n, fits) in t.notes.iter().zip(&t.fits) {
        let cells: Vec<String> = fits
            .iter()
            .map(|f| f.as_ref().map_or(format!("{:>7}", "-"), |f| format!("{:7.2}", f.slope)))
            .collect();
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
            println!("\nparams: decay={:.5} brightness={:.4} stable={}", p[0], p[1], is_stable(p[0]));
            for (label, t) in [("train", &train), ("held-out", &held)] {
                println!("
{label} render slopes over the same spans (log-energy/s):");
                for (n, r) in t.notes.iter().zip(t.rendered_slopes(&p)) {
                    let cells: Vec<String> =
                        r.iter().map(|v| v.map_or(format!("{:>7}", "-"), |v| format!("{v:7.2}"))).collect();
                    println!("  t={:7.3}s f0={:5.1}Hz  {}", n.t, n.f0, cells.join(" "));
                }
                print_table(label, t, &[("params", t.band_errors(&p))]);
            }
        }
        "grid" => {
            let brightness: f64 = args.get(2).map_or(1.0, |b| b.parse().expect("brightness"));
            let (train, held) = targets();
            println!("brightness={brightness:.4}   mean error: train / held-out / all 10 notes");
            // The baseline itself fails the stability bound; score it unconstrained.
            let base = [mean_scored(&train.band_errors(&BASELINE)), mean_scored(&held.band_errors(&BASELINE))];
            println!("  baseline {:.4}/{:.2}  {:.3} / {:.3} / {:.3}  (unstable at E2)", BASELINE[0], BASELINE[1], base[0], base[1], 0.5 * (base[0] + base[1]));
            for decay in [0.980, 0.982, 0.984, 0.985, 0.986, 0.987, 0.988, 0.989, 0.990, 0.991, 0.992, 0.993, 0.994, 0.995, 0.996, 0.997, 0.9975] {
                let p = [decay, brightness];
                if !is_stable(decay) {
                    println!("  decay {decay:.4}  unstable (loop gain bound >= 1 - eps)");
                    continue;
                }
                let (t, h) = (train.loss(&p), held.loss(&p));
                println!("  decay {decay:.4}  {t:.3} / {h:.3} / {:.3}", 0.5 * (t + h));
            }
        }
        "fit" => fit(&args[2..]),
        "sustain" => {
            let p = params_arg(&args[2..]);
            println!("params: decay={:.5} brightness={:.4}", p[0], p[1]);
            for (name, f) in OPEN_STRINGS {
                println!(
                    "  {name}  {f:>6.2} Hz  loop gain bound {:.6}  T60 ~ {:6.2} s",
                    loop_gain_bound(p[0] as f32, f),
                    t60_seconds(&p, f)
                );
            }
        }
        "phrase" => {
            let out = args.get(2).expect("usage: phrase <out.wav> [decay brightness]");
            let p = params_arg(&args[3..]);
            write_wav(out, &render_phrase(&p));
            println!("wrote {out} (decay={:.5} brightness={:.4})", p[0], p[1]);
        }
        _ => {
            eprintln!("usage: guitar-damping-fit eval|grid|fit|sustain|phrase ... (see src/main.rs)");
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
    let floors = band_noise_floors(&reference);
    (Target::new(&reference, &floors, &TRAIN), Target::new(&reference, &floors, &HELD_OUT))
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

    let mut opt = CmaEs::new(Array1::from(to_unit(&[0.99, BASELINE[1]]).to_vec()), 0.2, seed)
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
    println!("\nbaseline: decay={:.5} brightness={:.4} (stable={})", BASELINE[0], BASELINE[1], is_stable(BASELINE[0]));
    println!("fitted:   decay={:.5} brightness={:.4}  (seed {seed}, {generations} generations)", best[0], best[1]);
    print_table("train", &train, &[("baseline", train_before), ("fitted", train_after)]);
    print_table("held-out", &held, &[("baseline", held_before.clone()), ("fitted", held_after.clone())]);
    let (before, after) = (mean_scored(&held_before), mean_scored(&held_after));
    let verdict = if after < before { "IMPROVES" } else { "DOES NOT IMPROVE" };
    println!("\nheld-out verdict: fitted {verdict} on baseline ({before:.3} -> {after:.3})");
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

#[cfg(test)]
mod tests {
    use super::*;

    /// RMS of the rendered note in `[a, b)` seconds.
    fn window_rms(x: &[f64], a: f64, b: f64) -> f64 {
        rms(&x[(a * SR) as usize..(b * SR) as usize])
    }

    #[test]
    fn stability_bound_rejects_the_pre_fit_default() {
        // E2 bound at 0.9978 is 1.000096 in f32: the old type-0 default fails.
        assert!(loop_gain_bound(0.9978, 82.41) >= 1.0);
        assert!(!is_stable(BASELINE[0]));
        assert!(is_stable(0.990));
        // A2 / D3 sit just below 1.0 at the old default (0.99982 / 0.99908).
        assert!(loop_gain_bound(0.9978, 110.0) < 1.0);
        assert!(loop_gain_bound(0.9978, 146.83) < 1.0);
    }

    #[test]
    fn stability_bound_agrees_with_the_render() {
        // Rejected: at the UI slider max the low string does not decay.
        assert!(!is_stable(0.9999));
        let grows = render_note(&[0.9999, 1.0], 82.41, 8.0);
        assert!(window_rms(&grows, 7.0, 8.0) / window_rms(&grows, 4.0, 5.0) > 0.9);
        // Accepted: a stable setting decays by several dB over the same span.
        assert!(is_stable(0.990));
        let decays = render_note(&[0.990, 1.0], 82.41, 8.0);
        assert!(window_rms(&decays, 7.0, 8.0) / window_rms(&decays, 4.0, 5.0) < 0.5);
    }

    #[test]
    fn fit_rejects_unstable_candidates() {
        let empty = Target { notes: &[], fits: vec![vec![]] };
        assert_eq!(empty.loss(&[0.9995, 1.0]), REJECTED);
    }

    #[test]
    fn floor_truncation_recovers_a_decay_that_the_full_window_flattens() {
        // A 2 kHz partial decaying at -6 log-energy/s into a constant noise floor
        // ~45 dB below its start; most of the 3 s window is floor, which pulls the
        // untruncated fit toward 0.
        let true_slope = -6.0;
        let len = (3.0 * SR) as usize;
        let mut seed = 12345u32;
        let noise: Vec<f64> = (0..2 * len)
            .map(|_| {
                seed = seed.wrapping_mul(1664525).wrapping_add(1013904223);
                ((seed >> 8) as f64 / (1u32 << 24) as f64 - 0.5) * 0.02
            })
            .collect();
        let x: Vec<f64> = (0..len)
            .map(|i| {
                let t = i as f64 / SR;
                (0.5 * true_slope * t).exp() * (std::f64::consts::TAU * 2000.0 * t).sin() + noise[i]
            })
            .collect();
        let band = 4; // 1600-3500 Hz
        let edges = default_band_edges();
        let naive = band_decay_slope(&x, SR, edges[band], edges[band + 1], DEFAULT_WINDOW, DEFAULT_HOP);
        let floor_track = band_energy_track(&noise[len..], DEFAULT_HOP);
        let floor = floor_track.iter().map(|f| f[band]).sum::<f64>() / floor_track.len() as f64;
        let track = band_energy_track(&x, DEFAULT_HOP);
        let fit = fit_band(&x, &track, band, floor).expect("band clears the floor");
        assert!((fit.slope - true_slope).abs() < 0.1 * true_slope.abs(), "truncated {}", fit.slope);
        assert!((naive - true_slope).abs() > 3.0 * (fit.slope - true_slope).abs(), "naive {naive}");
    }

    #[test]
    fn bands_that_do_not_decay_are_dropped() {
        let x: Vec<f64> = (0..(0.8 * SR) as usize)
            .map(|i| (std::f64::consts::TAU * 2000.0 * i as f64 / SR).sin())
            .collect();
        let track = band_energy_track(&x, DEFAULT_HOP);
        assert!(fit_band(&x, &track, 4, 1e-12).is_none());
    }
}
