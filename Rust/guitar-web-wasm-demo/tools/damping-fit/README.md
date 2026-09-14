# damping-fit

Fits the guitar engine's loop-damping parameters (`decay`, `brightness` for
guitar type 0) so rendered notes decay per frequency band like
`reference/by-the-lake.wav`. It uses IX's
[`ix-acoustic-tune`](https://github.com/GuitarAlchemist/ix/tree/main/crates/ix-acoustic-tune)
(CMA-ES and `reference::per_band_decay_slopes`) and links `rust-engine`
natively through its C ABI, so the fitted values mean the same thing in the WASM
demo. Runs are seeded and deterministic, and a full fit takes about 25 s.

```sh
cd Rust/guitar-web-wasm-demo/tools/damping-fit
cargo run --release -- fit                       # train on 5 notes, report held-out
cargo run --release -- fit --swap                # the other cross-validation fold
cargo run --release -- eval 0.9900 0.9345        # per-band error for any params
cargo run --release -- sustain 0.9900 0.9345     # open-string T60 (extrapolation check)
cargo run --release -- phrase ../../playwright-downloads/phrase.wav 0.9900 0.9345
```

## How it measures

- **Target:** the 10 cleanest single-note runs in the recording, as found by
  IX's `analyze_reference` example. They are split by passage into 5 train
  notes and 5 held-out notes, so the three plucks at 64-66 s all fall on the
  held-out side.
- **Metric:** per-band decay slope (log-energy/s) over bands 60-180-360-750-1600-3500-8000 Hz.
  Both the recording and the render are measured from the RMS peak over the
  same duration. The loss is the mean absolute slope error across notes and bands.
- **Critic:** `phrase` renders the note sequence that
  `scripts/record-and-analyze.js` plays. Point
  `playwright-downloads/iteration-report.json` at the file
  (`{"wav_path": "<abs path>"}`), then run `node scripts/run-spectral-critic.js`.

## Result (2026-09-14, ix rev `d1bbf1d`, seed 7)

| per-band abs. decay error | baseline 0.9978 / 0.80 | fitted 0.9900 / 0.9345 |
|---|---|---|
| 60-180 Hz (held-out) | 1.337 | 0.571 |
| 180-360 Hz | 0.914 | 1.048 |
| 360-750 Hz | 1.051 | 0.570 |
| 750-1600 Hz | 1.823 | 0.921 |
| 1600-3500 Hz | 4.484 | 1.825 |
| 3500-8000 Hz | 6.935 | 4.286 |
| **held-out mean** | **2.757** | **1.537** |
| train mean | 2.851 | 2.189 |
| `run-spectral-critic.js` score | 0.6498 | 0.6769 |

Robustness checks:
- Seeds 7 and 11 reach the same held-out error (1.537). Seed 3 stops at
  decay 0.9948 and scores 1.691.
- The swapped fold (fit on the held-out notes) also improves: 2.851 to 2.272.

## Caveats (read before tuning further)

- **Low register only.** The recording has no clean single notes above about
  110 Hz, so nothing validates the upper strings. `decay` is a per-pass gain, so
  its effect on decay time grows with pitch. Open-string T60 moves from
  E2 9.3 / A2 121 / D3 31 / G3 10 / B3 5.8 / E4 2.7 s to
  10.6 / 7.7 / 4.9 / 3.1 / 2.2 / 1.3 s. The baseline's A2 and D3 values are
  implausible, because the low-string loop gain was at or above 1.0. The shorter
  E4 is not validated. Listen before building on it.
- **`decay` sits on its lower bound** (0.990, the demo's Decay slider minimum).
  The train fold wants it lower still, while the swapped fold settled at 0.996.
  Treat decay as loosely identified.
- **`brightness` is flat above about 0.84.** The per-voice brightness clamps
  at 1.0, which removes the dark `lp_alpha = 0.05` path. Below about 0.82 the
  error rises steeply.
- **3500-8000 Hz stays wrong (4.3).** The fixed two-point Karplus-Strong average
  in the loop removes high frequencies faster than any exposed parameter can
  offset. Closing that gap needs an engine change: decouple the fractional delay
  from the damping filter and expose the damping as a tunable parameter. Then
  refit with this harness.
- The reference slopes are noisy: it is a real performance with a noise floor
  and overlapping notes, and some high-band slopes come out positive. The note
  set is small.

## En français

`damping-fit` ajuste `decay` et `brightness` du profil 0 (CMA-ES
d'`ix-acoustic-tune`) pour que la décroissance par bande de fréquence d'une note
rendue suive `by-the-lake.wav`. Sur les notes de validation, l'erreur moyenne
passe de 2,757 à 1,537 log-énergie/s, et le score du critique spectral JS passe
de 0,650 à 0,677. Limites : seules des notes graves (86 à 110 Hz) sont
disponibles pour valider, et `decay` est en butée à 0,990. La bande
3,5 à 8 kHz reste mal reproduite : il faut une modification du moteur
(filtre d'amortissement réglable) avant un nouvel ajustement.
