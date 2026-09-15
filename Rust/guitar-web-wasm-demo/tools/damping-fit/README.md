# damping-fit

Fits the guitar engine's loop-damping parameters (`decay` and `brightness`, guitar
type 0) so a rendered note's per-band decay matches clean notes in
`reference/by-the-lake.wav`. It uses IX's
[`ix-acoustic-tune`](https://github.com/GuitarAlchemist/ix/tree/main/crates/ix-acoustic-tune)
(`CmaEs`, and `reference::band_decay_slope` as the tested reference for the
slope) plus `ix-signal`, both pinned to ix `d1bbf1d`. The engine source
(`../../rust-engine/src/lib.rs`) is compiled into the tool with a `#[path]`
module, so the shipped WASM is built exactly as before. Runs are seeded and
deterministic.

```sh
cd Rust/guitar-web-wasm-demo/tools/damping-fit
cargo test --release         # stability, 60 s renders at every profile + slider max, UI table, metric
cargo run --release -- grid  # decay picked on one fold, scored on the other (both directions)
cargo run --release -- sweep # the same pick across 96 noise-floor settings: median / range of the gain
cargo run --release -- eval 0.986 1.0     # reference + render slopes, per-band error, pair counts
cargo run --release -- sustain 0.986 1.0  # loop-gain bound + open-string T60
cargo run --release -- fit [--swap]       # CMA-ES on one fold (agrees with the grid)
cargo run --release -- phrase ../../playwright-downloads/phrase.wav 0.986 1.0
```

## How it measures

- **Notes:** the 10 cleanest single-note runs from IX's `analyze_reference`, split
  by passage into 5 train and 5 held-out notes. 8 of the 10 are the same pitch,
  about 97 Hz.
- **Slope:** per-band decay slope in log-energy/s, over bands
  60-180-360-750-1600-3500-8000 Hz, from the RMS peak.
  - Only early decay is covered: 0.35-0.8 s per note.
- **Noise floor (`METRIC`):**
  - Each band's floor is the p05 of whole-recording band energy.
  - A reference band is fitted only while it stays 10 dB above that floor.
  - A (note, band) pair is dropped if it lasts < 0.1 s, has a slope ≥ 0, or its
    fitted drop is < 3 dB.
  - A band counts only if ≥ 2 notes survive. The error is the median across
    notes, then the mean over counted bands.
  - The render is measured over the same frames as the reference.
- **Few pairs, different bands per fold.** Train is scored on **8 pairs in 3
  bands**, held-out on **15 pairs in 5 bands**. Every table prints these counts.
  Because the bands differ, train and held-out means can't be compared with each
  other; compare each only against its own baseline.
- **Stability:** `is_stable` rejects any decay where the loop-gain bound
  `(decay + 0.0025·(1-f_norm))·sustain` reaches the engine's `MAX_LOOP_GAIN`
  (1 - 1e-4) for any pitch from 82 to 1319 Hz. The fit can't depend on the
  engine's clamp.
- **Critic:** `phrase` renders the `scripts/record-and-analyze.js` sequence.
  Point `playwright-downloads/iteration-report.json` at the file, then run
  `node scripts/run-spectral-critic.js`.
  - The critic compares global spectra over the first 4 s. It does **not**
    measure decay.

## Result (2026-09-14)

The decay is **picked on the train notes only**, from a grid of 0.980-0.9975
in steps of 0.0005, at brightness 1.0. Scores are mean error (log-energy/s),
against the old default 0.9978 / 0.80.

| | pick | held-out: baseline → pick |
|---|---|---|
| train → held-out, `METRIC` | **0.986** | 2.699 → **0.802 (-70%)** |
| held-out → train (swapped) | 0.986 | 2.590 → 1.237 (-52%) |
| `METRIC` without the 3 dB drop rule | 0.986 | 2.403 → 1.250 (-48%); swapped 0.987, -47% |

**The -70% headline is the best case.** `METRIC` was fixed in `2a430db2` before
the first evaluation, but git history can't prove that. `sweep` repeats the
train-only pick across 96 settings: margin {6, 8, 10, 12} dB × percentile
{1, 2, 5}% × minimum drop {0, 3} dB × minimum notes {1, 2} × median or mean.

- **Held-out gain:** median **49%**, range 23-70%. `METRIC` ranks **1st of 96**.
- **The 3 dB drop rule** is the largest single lever. It filters on how steep the
  reference slope is, which favours fast decays. The -48% row is the fairer
  single number.
- **What holds in all 96 settings:** the train pick lands in 0.981-0.9875.
  Held-out beats both the old default and brightness-only.

Attribution under `METRIC`, held-out error, with the JS critic:

| decay / brightness | held-out | critic |
|---|---|---|
| 0.9978 / 0.80 (old) | 2.699 | 0.6499 |
| 0.9978 / 1.0 (brightness only; E2 unstable) | 2.133 | 0.6646 |
| 0.986 / 0.80 (decay only) | 3.661 | 0.6489 |
| **0.986 / 1.0 (shipped)** | **0.802** | **0.6821** |

- **Neither change works alone.**
- **The engine clamp shifted the baseline slightly.** The old default's
  low-string loop gain is now capped at 1 - 1e-4, so its error moved from 2.693
  to 2.699.
- **Why CMA-ES isn't used here:** brightness saturates, so the problem is 1-D
  and the grid is enough. CMA-ES (`fit`) finds 0.986-0.988. It is kept for the
  multi-parameter damping filter a future engine change would expose.

## What brightness actually changes

- **Only low notes.** Per-voice brightness is `base + 0.45·f_norm + 0.3·pluck_mix`.
  At base 0.80 it already saturates at 1.0 from about 110 Hz (velocity 1) to
  about 161 Hz (velocity 0.1).
  - Below that, brightness 1.0 turns off a large, pitch-dependent HF damping on
    the low strings.
  - Above that, brightness 0.80 and 1.0 render identically: G3, E4 and E5 are
    bit-identical.
- **9 of the 10 target notes (86-98 Hz)** are in the range where brightness acts.
- **Above about G2 the change is decay-only.** It is extrapolated from one note,
  the 110.3 Hz train note, which does support it: its 750-1600 and 1600-3500 Hz
  errors drop from 2.76 and 1.88 to 0.84 and 0.56.
- **Open-string T60 at 0.986 / 1.0:** E2 7.0, A2 5.1, D3 3.4, G3 2.3, B3 1.7 s,
  and **E4 1.1 s (unverified)**. At the old default: E2 9.3, A2 121, D3 31, G3 10,
  B3 5.8, E4 2.7 s.

## Engine stability (fixed in this PR)

- **The bug:** every loop stage has gain 1 at DC, so `decay·sustain ≥ 1` grows
  the low-string DC mode without limit. It was never damped in practice.
  - Profiles 1-3 reached full scale on E2 in 45-85 s.
  - Slider max (0.9999) produced NaN at about 500 s and silenced the engine
    until reload.
  - With brightness 1.0, the slider max became an audible full-scale tone.
- **The fix:** `render()` now clamps the per-voice loop gain to `MAX_LOOP_GAIN`
  (1 - 1e-4). This affects only settings whose bound was already ≥ 1 - 1e-4.
  The new type-0 defaults are well below it.
- **The test:** `long_renders_stay_finite_and_bounded_at_profile_defaults_and_slider_max`
  renders 60 s for each profile, at its default and at the slider max.
  - It checks the output stays finite, RMS shrinks, and DC stays under 0.1.
  - With the clamp removed, it fails for 6 of the 8 cases.

## Other caveats

- 3.5-8 kHz: the residual is about 1.8-2.2 on 2-3 notes. The render decays
  faster in 4 of 5 notes. That is weak evidence for a tunable loop filter, not
  proof.
- Most (note, band) pairs are dropped because the recording is dense.
- `src/atoms/audioAtoms.js` `GUITAR_PROFILE_DECAY` must match the engine
  profiles. `ui_decay_table_matches_engine_profiles` pins it by rendering.

## En français

`damping-fit` ajuste `decay` et `brightness` du profil 0 sur la décroissance par
bande de `by-the-lake.wav`, avec gestion du plancher de bruit.

- **Choix de `decay`:** fait sur les seules notes d'entraînement. Il vaut 0,986,
  ce qui donne -70 % d'erreur en validation et -52 % dans le sens inverse.
- **Robustesse:** sur 96 réglages du plancher de bruit, le gain médian est de
  49 % (entre 23 et 70 %). Les réglages par défaut sont le meilleur cas.
- **Portée de `brightness`:** elle n'agit que sous environ 110-160 Hz. Au-dessus,
  le changement porte seulement sur `decay`, extrapolé d'une seule note à 110 Hz.
  Le T60 du mi aigu (1,1 s) n'est pas vérifié.
- **Stabilité du moteur:** le moteur borne désormais le gain de boucle sous 1.
  Les profils 1-3 et le curseur au maximum ne divergent plus.
