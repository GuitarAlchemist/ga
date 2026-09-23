//! Fit the guitar engine's loop-damping parameters to the per-band decay of the
//! reference recording, using `ix-acoustic-tune` (CMA-ES, band decay slopes).
//!
//! The engine source is compiled straight into this binary (`#[path]` module
//! below), so a fitted parameter set means the same thing in the browser without
//! changing how the shipped WASM is built. Everything is seeded and deterministic.
//!
//! Usage (from this directory):
//!   cargo run --release -- grid    [brightness]        train-only decay pick, scored on the other fold
//!   cargo run --release -- sweep                       the same pick across 96 noise-floor settings
//!   cargo run --release -- eval    [decay brightness]  reference/render slopes, per-band error, pair counts
//!   cargo run --release -- fit     [--generations N] [--seed S] [--swap]   CMA-ES instead of the grid
//!   cargo run --release -- sustain [decay brightness]  loop-gain bound + open-string T60
//!   cargo run --release -- phrase  <out.wav> [decay brightness]
//!
//! `phrase` renders the note sequence `scripts/record-and-analyze.js` plays, so
//! `scripts/run-spectral-critic.js` can score it (see README.md).

#[allow(dead_code, clippy::all)]
#[path = "../../../rust-engine/src/lib.rs"]
mod engine;

use engine::{
    engine_init, engine_note_on, engine_render, engine_set_brightness, engine_set_decay,
    engine_set_guitar_type, Engine, MAX_LOOP_GAIN,
};
use ix_acoustic_tune::cmaes::CmaEs;
use ix_acoustic_tune::reference::{default_band_edges, DEFAULT_HOP, DEFAULT_WINDOW};
use ix_acoustic_tune::AskTell;
use ndarray::Array1;

const REFERENCE_WAV: &str = "../../reference/by-the-lake.wav";
const SR: f64 = 48_000.0;
/// Guitar type the demo starts on (App.jsx `useState(0)`, steel bright).
const GUITAR_TYPE: i32 = 0;
/// `[decay, brightness]` guitar type 0 shipped before this fit (the "baseline").
const BASELINE: [f64; 2] = [0.9978, 0.80];
/// Brightness-only alternative (unstable at E2; scored for attribution only).
const BRIGHTNESS_ONLY: [f64; 2] = [0.9978, 1.0];

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
// same ~97 Hz pitch; only the 110.3 Hz note sits where brightness 0.80 already
// saturates, so it is the only direct evidence for the decay-only change above G2.
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
/// A (note, band) pair needs at least this much time above the noise floor.
const MIN_FIT_SECONDS: f64 = 0.10;

/// Early decay only: the reference rings 0.38-1.03 s before the next pluck, so
/// slopes describe the first ~0.8 s, not a multi-second T60.
fn analysis_seconds(n: &Note) -> f64 {
    n.ring.clamp(0.35, 0.8)
}

/// Decay candidates for `grid` and `sweep`: 0.980..=0.9975 in 0.0005 steps. The
/// top is the highest stable value on the demo slider's 0.0001 step.
fn decay_grid() -> Vec<f64> {
    (0..36).map(|i| 0.980 + 0.0005 * i as f64).collect()
}

// ------------------------------------------------------------------ metric --

/// How reference slopes are cleaned before scoring. `METRIC` is what the tool
/// uses by default; `sweep` reports the result across the whole family, because
/// a single setting can be (and at `METRIC` is) the most favourable one.
#[derive(Clone, Copy)]
struct Metric {
    /// Fit a reference band only while it stays this far above its noise floor.
    margin_db: f64,
    /// Percentile of whole-recording band energy taken as the noise floor.
    floor_percentile: f64,
    /// Drop a (note, band) whose fitted reference decay falls by less than this.
    /// It filters on the reference slope's steepness, which favours fast decays.
    min_drop_db: f64,
    /// A band only counts if this many notes survive in it.
    min_notes: usize,
    /// Median (true) or mean (false) of |error| across notes.
    median: bool,
}

const METRIC: Metric =
    Metric { margin_db: 10.0, floor_percentile: 0.05, min_drop_db: 3.0, min_notes: 2, median: true };

/// Per-frame energy in each band: `[frame][band]`.
type Track = Vec<Vec<f64>>;

/// Band energies with the same STFT and bin mapping as
/// `ix_acoustic_tune::reference::band_decay_slope`.
fn band_track(x: &[f64], hop: usize) -> Track {
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

/// Log-energy decay slope of band `b` over the first `frames` frames. Identical
/// to `band_decay_slope` on the samples spanning those frames (tested), without
/// recomputing the STFT per band and per span.
fn slope_over(track: &Track, b: usize, frames: usize) -> f64 {
    let dt = DEFAULT_HOP as f64 / SR;
    let pts: Vec<(f64, f64)> = track[..frames.min(track.len())]
        .iter()
        .enumerate()
        .filter(|(_, f)| f[b] > 1e-20)
        .map(|(i, f)| (i as f64 * dt, f[b].ln()))
        .collect();
    let n = pts.len() as f64;
    if n < 2.0 {
        return 0.0;
    }
    let (sx, sy) = pts.iter().fold((0.0, 0.0), |(a, c), (x, y)| (a + x, c + y));
    let (sxx, sxy) = pts.iter().fold((0.0, 0.0), |(a, c), (x, y)| (a + x * x, c + x * y));
    let denom = n * sxx - sx * sx;
    if denom.abs() < 1e-20 { 0.0 } else { (n * sxy - sx * sy) / denom }
}

/// Whole-recording band energies, sorted, for noise-floor percentiles (a real
/// performance never reaches silence; a render's floor is zero).
struct Floors(Vec<Vec<f64>>);

impl Floors {
    fn new(recording: &[f64]) -> Self {
        let track = band_track(recording, DEFAULT_WINDOW);
        Self(
            (0..track[0].len())
                .map(|b| {
                    let mut e: Vec<f64> = track.iter().map(|f| f[b]).collect();
                    e.sort_by(f64::total_cmp);
                    e
                })
                .collect(),
        )
    }

    fn at(&self, b: usize, percentile: f64) -> f64 {
        let e = &self.0[b];
        e[((e.len() - 1) as f64 * percentile) as usize]
    }
}

/// A reference slope kept for scoring, and how many frames it was fitted over.
struct BandFit {
    slope: f64,
    frames: usize,
}

/// Fit band `b` from the envelope peak until it first drops to `floor + margin`;
/// `None` if that is under `MIN_FIT_SECONDS`, the slope is not negative, or the
/// fitted drop is under `min_drop_db`.
fn fit_band(track: &Track, b: usize, floor: f64, m: &Metric) -> Option<BandFit> {
    let threshold = floor * 10f64.powf(m.margin_db / 10.0);
    let frames = track.iter().position(|f| f[b] <= threshold).unwrap_or(track.len());
    let seconds = frames.saturating_sub(1) as f64 * DEFAULT_HOP as f64 / SR;
    if seconds < MIN_FIT_SECONDS {
        return None;
    }
    let slope = slope_over(track, b, frames);
    let drop_db = -slope * seconds * 10.0 / std::f64::consts::LN_10;
    (slope < 0.0 && drop_db >= m.min_drop_db).then_some(BandFit { slope, frames })
}

// -------------------------------------------------------------- stability --

/// Upper bound of the engine's per-voice loop gain at pitch `f` *before* the
/// engine's own `MAX_LOOP_GAIN` clamp, mirroring `render()`:
/// `(decay + 0.0025·(1-f_norm))·sustain`. The KS average, dispersion allpass and
/// LP/bright mix all have |H| <= 1 with equality at DC, so the bound is tight.
fn loop_gain_bound(decay: f32, f: f32) -> f32 {
    let f_norm = ((f.clamp(82.0, 330.0) - 82.0) / (330.0 - 82.0)).clamp(0.0, 1.0);
    let sustain = (0.995 + 0.006 * (1.0 - f_norm)).min(0.9998);
    (decay.clamp(0.95, 0.9999) + 0.0025 * (1.0 - f_norm)) * sustain
}

/// True if the engine's clamp never engages for any pitch the demo can play
/// (E2 to the 12-string's high E, 82-1319 Hz). A fit must not lean on the clamp.
fn is_stable(decay: f64) -> bool {
    (82..=1319).all(|f| loop_gain_bound(decay as f32, f as f32) < MAX_LOOP_GAIN)
}

// ---------------------------------------------------------------- engine --

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

/// Render one pluck at `f0` (velocity 1.0, as the demo's buttons do).
fn render_note(p: &[f64; 2], f0: f64, seconds: f64) -> Vec<f64> {
    let eng = new_engine(p);
    engine_note_on(eng, f0 as f32, 1.0);
    let out = render(eng, (seconds * SR) as usize);
    free_engine(eng);
    out.into_iter().map(f64::from).collect()
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

fn peak_track(x: &[f64], n: &Note) -> Track {
    let p = peak_index(x);
    band_track(&x[p..(p + (analysis_seconds(n) * SR) as usize).min(x.len())], DEFAULT_HOP)
}

/// Band tracks of the notes rendered at `p`, one thread per note.
fn render_tracks(notes: &[Note], p: &[f64; 2]) -> Vec<Track> {
    std::thread::scope(|s| {
        let handles: Vec<_> = notes
            .iter()
            .map(|n| {
                s.spawn(move || {
                    let secs = PEAK_SEARCH_S + analysis_seconds(n) + 0.05;
                    peak_track(&render_note(p, n.f0, secs), n)
                })
            })
            .collect();
        handles.into_iter().map(|h| h.join().unwrap()).collect()
    })
}

// ------------------------------------------------------------------ scoring --

/// Reference band tracks of a note set (independent of the metric settings).
struct Notes {
    label: &'static str,
    notes: &'static [Note],
    tracks: Vec<Track>,
}

impl Notes {
    fn new(recording: &[f64], label: &'static str, notes: &'static [Note]) -> Self {
        let tracks = notes
            .iter()
            .map(|n| {
                let start = (n.t * SR) as usize;
                let end = start + ((PEAK_SEARCH_S + analysis_seconds(n)) * SR) as usize;
                peak_track(&recording[start..end], n)
            })
            .collect();
        Self { label, notes, tracks }
    }
}

/// A note set scored under one metric setting.
struct Target<'a> {
    set: &'a Notes,
    metric: Metric,
    /// `fits[note][band]`, `None` where the reference band was dropped.
    fits: Vec<Vec<Option<BandFit>>>,
}

impl<'a> Target<'a> {
    fn new(set: &'a Notes, floors: &Floors, metric: Metric) -> Self {
        let fits = set
            .tracks
            .iter()
            .map(|t| (0..floors.0.len()).map(|b| fit_band(t, b, floors.at(b, metric.floor_percentile), &metric)).collect())
            .collect();
        Self { set, metric, fits }
    }

    fn bands(&self) -> usize {
        self.fits.first().map_or(0, Vec::len)
    }

    fn survivors(&self, b: usize) -> usize {
        self.fits.iter().filter(|f| f[b].is_some()).count()
    }

    /// `(pairs, bands)` that actually enter the loss.
    fn scored(&self) -> (usize, usize) {
        let counted: Vec<usize> =
            (0..self.bands()).map(|b| self.survivors(b)).filter(|&n| n >= self.metric.min_notes).collect();
        (counted.iter().sum(), counted.len())
    }

    /// Render slopes over the same frames as each kept reference slope.
    fn render_slopes(&self, rendered: &[Track]) -> Vec<Vec<Option<f64>>> {
        rendered
            .iter()
            .zip(&self.fits)
            .map(|(t, fits)| {
                fits.iter().enumerate().map(|(b, f)| Some(slope_over(t, b, f.as_ref()?.frames))).collect()
            })
            .collect()
    }

    /// Per-band |slope error| across notes (median or mean); `None` for bands with
    /// fewer than `min_notes` surviving notes.
    fn band_errors(&self, rendered: &[Track]) -> Vec<Option<f64>> {
        let slopes = self.render_slopes(rendered);
        (0..self.bands())
            .map(|b| {
                let mut errs: Vec<f64> = slopes
                    .iter()
                    .zip(&self.fits)
                    .filter_map(|(r, fits)| Some((r[b]? - fits[b].as_ref()?.slope).abs()))
                    .collect();
                (errs.len() >= self.metric.min_notes.max(1)).then(|| {
                    if self.metric.median { median(&mut errs) } else { errs.iter().sum::<f64>() / errs.len() as f64 }
                })
            })
            .collect()
    }

    fn score(&self, rendered: &[Track]) -> f64 {
        mean_scored(&self.band_errors(rendered))
    }

    /// Render and score; unstable parameters are rejected outright.
    fn loss(&self, p: &[f64; 2]) -> f64 {
        if !is_stable(p[0]) {
            return REJECTED;
        }
        self.score(&render_tracks(self.set.notes, p)).min(REJECTED)
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

fn print_table(t: &Target, rows: &[(&str, Vec<Option<f64>>)]) {
    let edges = default_band_edges();
    let (pairs, bands) = t.scored();
    println!(
        "\n{} — per-band {} |decay slope error| (log-energy/s); scored: {pairs} pairs / {bands} bands",
        t.set.label,
        if t.metric.median { "median" } else { "mean" }
    );
    print!("  {:>14}  {:>5}", "band Hz", "notes");
    for (name, _) in rows {
        print!("  {name:>10}");
    }
    println!();
    for b in 0..edges.len() - 1 {
        print!("  {:>14}  {:>5}", format!("{:.0}-{:.0}", edges[b], edges[b + 1]), t.survivors(b));
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

fn print_slopes(label: &str, t: &Target, slopes: &[Vec<Option<f64>>]) {
    println!("\n{} {label} slopes (log-energy/s; '-' = dropped):", t.set.label);
    for (n, row) in t.set.notes.iter().zip(slopes) {
        let cells: Vec<String> =
            row.iter().map(|v| v.map_or(format!("{:>7}", "-"), |v| format!("{v:7.2}"))).collect();
        println!("  t={:7.3}s f0={:5.1}Hz  {}", n.t, n.f0, cells.join(" "));
    }
}

// ------------------------------------------------------------- selection --

/// Rendered tracks for every grid decay: `[decay][note]`.
fn render_grid(set: &Notes, brightness: f64) -> Vec<Vec<Track>> {
    decay_grid().iter().map(|&d| render_tracks(set.notes, &[d, brightness])).collect()
}

/// Decay picked on `fit` alone (first minimum on the grid), then scored on `check`.
struct Pick {
    decay: f64,
    check_before: f64,
    check_after: f64,
    check_brightness_only: f64,
}

impl Pick {
    fn gain(&self) -> f64 {
        1.0 - self.check_after / self.check_before
    }
}

struct Rendered {
    grid: Vec<Vec<Track>>,
    baseline: Vec<Track>,
    brightness_only: Vec<Track>,
}

impl Rendered {
    fn new(set: &Notes) -> Self {
        Self {
            grid: render_grid(set, 1.0),
            baseline: render_tracks(set.notes, &BASELINE),
            brightness_only: render_tracks(set.notes, &BRIGHTNESS_ONLY),
        }
    }
}

fn pick(fit: &Target, fit_r: &Rendered, check: &Target, check_r: &Rendered) -> Pick {
    let grid = decay_grid();
    let mut best = (0, f64::INFINITY);
    for (i, tracks) in fit_r.grid.iter().enumerate() {
        let l = fit.score(tracks);
        if l < best.1 {
            best = (i, l);
        }
    }
    Pick {
        decay: grid[best.0],
        check_before: check.score(&check_r.baseline),
        check_after: check.score(&check_r.grid[best.0]),
        check_brightness_only: check.score(&check_r.brightness_only),
    }
}

// --------------------------------------------------------------------- main --

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let cmd = args.get(1).map(String::as_str).unwrap_or("");
    match cmd {
        "eval" => {
            let p = params_arg(&args[2..]);
            let (recording, floors) = load();
            println!("params: decay={:.5} brightness={:.4} stable={}", p[0], p[1], is_stable(p[0]));
            for set in [Notes::new(&recording, "train", &TRAIN), Notes::new(&recording, "held-out", &HELD_OUT)] {
                let t = Target::new(&set, &floors, METRIC);
                let rendered = render_tracks(set.notes, &p);
                let reference: Vec<Vec<Option<f64>>> =
                    t.fits.iter().map(|r| r.iter().map(|f| f.as_ref().map(|f| f.slope)).collect()).collect();
                print_slopes("reference", &t, &reference);
                print_slopes("render", &t, &t.render_slopes(&rendered));
                print_table(&t, &[("params", t.band_errors(&rendered))]);
            }
        }
        "grid" => grid(args.get(2).map_or(1.0, |b| b.parse().expect("brightness"))),
        "sweep" => sweep(),
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
            eprintln!("usage: guitar-damping-fit grid|sweep|eval|fit|sustain|phrase ... (see src/main.rs)");
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

fn load() -> (Vec<f64>, Floors) {
    let recording = load_wav_mono(REFERENCE_WAV);
    let floors = Floors::new(&recording);
    (recording, floors)
}

/// Decay picked on one fold only, reported on the other, in both directions.
fn grid(brightness: f64) {
    let (recording, floors) = load();
    let (train_set, held_set) = (Notes::new(&recording, "train", &TRAIN), Notes::new(&recording, "held-out", &HELD_OUT));
    let (train, held) = (Target::new(&train_set, &floors, METRIC), Target::new(&held_set, &floors, METRIC));
    let (tr, hr) = (render_grid(&train_set, brightness), render_grid(&held_set, brightness));
    let (tp, hp) = (train.scored(), held.scored());
    println!("brightness={brightness:.4}; scored pairs/bands: train {}/{}, held-out {}/{}", tp.0, tp.1, hp.0, hp.1);
    println!("(the two folds are scored on different bands, so their means are not comparable to each other)");
    println!("  {:>8}  {:>7}  {:>8}", "decay", "train", "held-out");
    for (i, d) in decay_grid().iter().enumerate() {
        println!("  {d:>8.4}  {:>7.3}  {:>8.3}", train.score(&tr[i]), held.score(&hr[i]));
    }
    if brightness == 1.0 {
        let (train_r, held_r) = (
            Rendered { grid: tr, baseline: render_tracks(&TRAIN, &BASELINE), brightness_only: render_tracks(&TRAIN, &BRIGHTNESS_ONLY) },
            Rendered { grid: hr, baseline: render_tracks(&HELD_OUT, &BASELINE), brightness_only: render_tracks(&HELD_OUT, &BRIGHTNESS_ONLY) },
        );
        for (fit_t, fit_r, check_t, check_r) in [(&train, &train_r, &held, &held_r), (&held, &held_r, &train, &train_r)] {
            let p = pick(fit_t, fit_r, check_t, check_r);
            println!(
                "\npicked on {} only: decay {:.4} -> {}: baseline {:.3} -> {:.3} ({:+.0}%), brightness-only {:.3}",
                fit_t.set.label,
                p.decay,
                check_t.set.label,
                p.check_before,
                p.check_after,
                -100.0 * p.gain(),
                p.check_brightness_only
            );
        }
    }
}

/// Train-only pick and held-out gain across 96 noise-floor settings.
fn sweep() {
    let (recording, floors) = load();
    let (train_set, held_set) = (Notes::new(&recording, "train", &TRAIN), Notes::new(&recording, "held-out", &HELD_OUT));
    let (train_r, held_r) = (Rendered::new(&train_set), Rendered::new(&held_set));
    let mut rows: Vec<(Metric, Pick, Pick)> = Vec::new();
    for margin_db in [6.0, 8.0, 10.0, 12.0] {
        for floor_percentile in [0.01, 0.02, 0.05] {
            for min_drop_db in [0.0, 3.0] {
                for min_notes in [1, 2] {
                    for median in [true, false] {
                        let m = Metric { margin_db, floor_percentile, min_drop_db, min_notes, median };
                        let (train, held) = (Target::new(&train_set, &floors, m), Target::new(&held_set, &floors, m));
                        rows.push((m, pick(&train, &train_r, &held, &held_r), pick(&held, &held_r, &train, &train_r)));
                    }
                }
            }
        }
    }
    println!("margin  pct  drop  n  agg     pick  held-out before->after  gain | swapped pick  gain");
    for (m, p, s) in &rows {
        println!(
            "{:>5.0}  {:>3.0}%  {:>4.0}  {}  {:<6}  {:.4}  {:>6.3} -> {:>6.3}  {:>4.0}% | {:.4}  {:>4.0}%",
            m.margin_db,
            m.floor_percentile * 100.0,
            m.min_drop_db,
            m.min_notes,
            if m.median { "median" } else { "mean" },
            p.decay,
            p.check_before,
            p.check_after,
            100.0 * p.gain(),
            s.decay,
            100.0 * s.gain()
        );
    }
    let mut gains: Vec<f64> = rows.iter().map(|(_, p, _)| p.gain()).collect();
    let is = |m: &Metric, drop: f64| {
        m.margin_db == METRIC.margin_db
            && m.floor_percentile == METRIC.floor_percentile
            && m.min_drop_db == drop
            && m.min_notes == METRIC.min_notes
            && m.median == METRIC.median
    };
    let default_gain = rows.iter().find(|(m, _, _)| is(m, METRIC.min_drop_db)).unwrap().1.gain();
    let rank = 1 + gains.iter().filter(|&&g| g > default_gain).count();
    let picks: Vec<f64> = rows.iter().map(|(_, p, _)| p.decay).collect();
    let beats_base = rows.iter().filter(|(_, p, _)| p.check_after < p.check_before).count();
    let beats_bright = rows.iter().filter(|(_, p, _)| p.check_after < p.check_brightness_only).count();
    let min_gain = gains.iter().copied().fold(f64::INFINITY, f64::min);
    let max_gain = gains.iter().copied().fold(f64::NEG_INFINITY, f64::max);
    println!("\n{} settings, decay picked on train only, gain measured on held-out:", rows.len());
    println!("  median gain {:.0}%  range {:.0}%..{:.0}%", 100.0 * median(&mut gains), 100.0 * min_gain, 100.0 * max_gain);
    println!("  default METRIC gain {:.0}% (rank {rank} of {})", 100.0 * default_gain, rows.len());
    println!(
        "  train picks {:.4}..{:.4}; held-out beats baseline in {beats_base}, beats brightness-only in {beats_bright}",
        picks.iter().copied().fold(f64::INFINITY, f64::min),
        picks.iter().copied().fold(f64::NEG_INFINITY, f64::max)
    );
    let (_, p, s) = rows.iter().find(|(m, _, _)| is(m, 0.0)).unwrap();
    println!(
        "  default without the 3 dB drop rule: pick {:.4}, held-out {:.3} -> {:.3} ({:+.0}%); swapped pick {:.4}, {:.3} -> {:.3} ({:+.0}%)",
        p.decay, p.check_before, p.check_after, -100.0 * p.gain(), s.decay, s.check_before, s.check_after, -100.0 * s.gain()
    );
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
    let (recording, floors) = load();
    let (train_set, held_set) = (Notes::new(&recording, "train", &TRAIN), Notes::new(&recording, "held-out", &HELD_OUT));
    // `--swap` fits on the held-out notes and validates on the train notes.
    let (mut train, mut held) = (Target::new(&train_set, &floors, METRIC), Target::new(&held_set, &floors, METRIC));
    if a.iter().any(|x| x == "--swap") {
        std::mem::swap(&mut train, &mut held);
    }

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
        println!("gen {:>3}  best {} loss {l:.4}  decay={:.5} brightness={:.4}", opt.generation(), train.set.label, p[0], p[1]);
    }
    let (u, _) = opt.recommend().unwrap();
    let best = to_params(u.as_slice().unwrap());
    println!("\nbaseline: decay={:.5} brightness={:.4} (stable={})", BASELINE[0], BASELINE[1], is_stable(BASELINE[0]));
    println!("fitted:   decay={:.5} brightness={:.4}  (seed {seed}, {generations} generations)", best[0], best[1]);
    for t in [&train, &held] {
        let rows = [
            ("baseline", t.band_errors(&render_tracks(t.set.notes, &BASELINE))),
            ("fitted", t.band_errors(&render_tracks(t.set.notes, &best))),
        ];
        print_table(t, &rows);
    }
}

/// CMA-ES searches a unit box; `decay` is searched on a log scale of `1 - decay`
/// in [0.0002, 0.03]. The top of that range is unstable on the low strings;
/// `Target::loss` rejects it via `is_stable`.
const DECAY_LOG_RANGE: (f64, f64) = (-3.506_557_897_319_982, -8.517_193_191_416_238); // ln 0.03, ln 0.0002

fn to_params(u: &[f64]) -> [f64; 2] {
    let (lo, hi) = DECAY_LOG_RANGE;
    [1.0 - (lo + u[0].clamp(0.0, 1.0) * (hi - lo)).exp(), u[1].clamp(0.0, 1.0)]
}

fn to_unit(p: &[f64; 2]) -> [f64; 2] {
    let (lo, hi) = DECAY_LOG_RANGE;
    [((1.0 - p[0]).ln() - lo) / (hi - lo), p[1]]
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
    use ix_acoustic_tune::reference::band_decay_slope;

    const SLIDER_MAX: f32 = 0.9999;

    fn window_rms(x: &[f64], a: f64, b: f64) -> f64 {
        rms(&x[(a * SR) as usize..(b * SR) as usize])
    }

    #[test]
    fn stability_bound_rejects_the_pre_fit_default() {
        // E2 bound at 0.9978 is 1.000096 in f32: the old type-0 default fails.
        assert!(loop_gain_bound(0.9978, 82.41) >= 1.0);
        assert!(!is_stable(BASELINE[0]));
        assert!(is_stable(0.986));
        assert!(is_stable(*decay_grid().last().unwrap()));
        // A2 / D3 sit just below 1.0 at the old default (0.99982 / 0.99908).
        assert!(loop_gain_bound(0.9978, 110.0) < 1.0);
        assert!(loop_gain_bound(0.9978, 146.83) < 1.0);
    }

    #[test]
    fn a_stable_setting_decays() {
        let x = render_note(&[0.986, 1.0], 82.41, 8.0);
        assert!(window_rms(&x, 7.0, 8.0) / window_rms(&x, 4.0, 5.0) < 0.5);
    }

    /// Before `MAX_LOOP_GAIN`, profiles 1-3 drove the E2 DC mode to full scale in
    /// 45-85 s and the slider max went NaN at ~500 s. With the clamp the loop gain
    /// is < 1, so a pluck must stay finite and shrink, at every profile default and
    /// at the slider max. Without the clamp this fails within 60 s (checked by
    /// removing it): profile 3 is at full scale by ~45 s.
    #[test]
    fn long_renders_stay_finite_and_bounded_at_profile_defaults_and_slider_max() {
        let cases: Vec<(i32, Option<f32>)> = (0..4).flat_map(|t| [(t, None), (t, Some(SLIDER_MAX))]).collect();
        std::thread::scope(|s| {
            let handles: Vec<_> = cases
                .iter()
                .map(|&(t, decay)| {
                    s.spawn(move || {
                        let eng = engine_init(SR as f32);
                        engine_set_guitar_type(eng, t);
                        if let Some(d) = decay {
                            engine_set_decay(eng, d);
                        }
                        engine_note_on(eng, 82.41, 1.0);
                        let x: Vec<f64> = render(eng, (60.0 * SR) as usize).into_iter().map(f64::from).collect();
                        free_engine(eng);
                        let label = format!("type {t} decay {decay:?}");
                        assert!(x.iter().all(|v| v.is_finite()), "{label}: non-finite output");
                        let (early, late) = (window_rms(&x, 1.0, 11.0), window_rms(&x, 50.0, 60.0));
                        assert!(late < early, "{label}: RMS grew {early:.4} -> {late:.4}");
                        let dc = x[x.len() - SR as usize..].iter().sum::<f64>() / SR;
                        assert!(dc.abs() < 0.1, "{label}: DC offset {dc:.3} in the last second");
                    })
                })
                .collect();
            handles.into_iter().for_each(|h| h.join().unwrap());
        });
    }

    #[test]
    fn ui_decay_table_matches_engine_profiles() {
        let js = std::fs::read_to_string("../../src/atoms/audioAtoms.js").unwrap();
        let l = js.lines().find(|l| l.contains("GUITAR_PROFILE_DECAY =")).unwrap();
        let table: Vec<f32> =
            l[l.find('[').unwrap() + 1..l.find(']').unwrap()].split(',').map(|v| v.trim().parse().unwrap()).collect();
        assert_eq!(table.len(), 4);
        for (t, &d) in table.iter().enumerate() {
            let run = |set: bool| {
                let e = engine_init(48_000.0);
                engine_set_guitar_type(e, t as i32);
                if set {
                    engine_set_decay(e, d);
                }
                engine_note_on(e, 82.41, 1.0);
                let x = render(e, 9_600);
                free_engine(e);
                x
            };
            assert_eq!(run(false), run(true), "guitar type {t}: JS decay {d} != engine profile");
        }
    }

    /// Per-voice brightness saturates at 1.0 from ~110 Hz (velocity 1) at base
    /// 0.80, so brightness 1.0 only changes the lowest notes: above that the
    /// shipped change is decay-only.
    #[test]
    fn brightness_change_only_reaches_low_notes() {
        let pair = |f: f64| (render_note(&[0.986, 0.80], f, 0.5), render_note(&[0.986, 1.0], f, 0.5));
        for f in [196.0, 329.63, 659.26] {
            let (a, b) = pair(f);
            assert_eq!(a, b, "{f} Hz should be bit-identical");
        }
        let (a, b) = pair(82.41);
        assert_ne!(a, b, "E2 should differ");
    }

    #[test]
    fn fit_rejects_unstable_candidates() {
        let set = Notes { label: "empty", notes: &[], tracks: vec![] };
        let t = Target { set: &set, metric: METRIC, fits: vec![] };
        assert_eq!(t.loss(&[0.9995, 1.0]), REJECTED);
    }

    #[test]
    fn track_slope_equals_the_crate_band_decay_slope() {
        let x: Vec<f64> = (0..(0.8 * SR) as usize)
            .map(|i| {
                let t = i as f64 / SR;
                (-2.0 * t).exp() * (std::f64::consts::TAU * 440.0 * t).sin()
                    + (-5.0 * t).exp() * (std::f64::consts::TAU * 2500.0 * t).sin()
            })
            .collect();
        let track = band_track(&x, DEFAULT_HOP);
        let edges = default_band_edges();
        for b in 0..edges.len() - 1 {
            for frames in [20, 57, track.len()] {
                let span = (frames - 1) * DEFAULT_HOP + DEFAULT_WINDOW;
                let crate_slope = band_decay_slope(&x[..span], SR, edges[b], edges[b + 1], DEFAULT_WINDOW, DEFAULT_HOP);
                assert!((slope_over(&track, b, frames) - crate_slope).abs() < 1e-9, "band {b} frames {frames}");
            }
        }
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
        let track = band_track(&x, DEFAULT_HOP);
        let naive = slope_over(&track, band, track.len());
        let floor_track = band_track(&noise[len..], DEFAULT_HOP);
        let floor = floor_track.iter().map(|f| f[band]).sum::<f64>() / floor_track.len() as f64;
        let fit = fit_band(&track, band, floor, &METRIC).expect("band clears the floor");
        assert!((fit.slope - true_slope).abs() < 0.1 * true_slope.abs(), "truncated {}", fit.slope);
        assert!((naive - true_slope).abs() > 3.0 * (fit.slope - true_slope).abs(), "naive {naive}");
    }

    #[test]
    fn bands_that_do_not_decay_are_dropped() {
        let x: Vec<f64> =
            (0..(0.8 * SR) as usize).map(|i| (std::f64::consts::TAU * 2000.0 * i as f64 / SR).sin()).collect();
        assert!(fit_band(&band_track(&x, DEFAULT_HOP), 4, 1e-12, &METRIC).is_none());
    }
}
