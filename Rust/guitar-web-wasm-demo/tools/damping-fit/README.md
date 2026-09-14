# damping-fit

Fits the guitar engine's loop-damping parameters (`decay`, `brightness` for
guitar type 0) so rendered notes decay per frequency band like
`reference/by-the-lake.wav`. It uses IX's
[`ix-acoustic-tune`](https://github.com/GuitarAlchemist/ix/tree/main/crates/ix-acoustic-tune)
(`CmaEs`, `reference::band_decay_slope`) and `ix-signal`, both pinned to ix
`d1bbf1d`. The engine source (`../../rust-engine/src/lib.rs`) is compiled into
the tool with a `#[path]` module, so `rust-engine`'s crate type and the shipped
WASM stay unchanged. Runs are seeded and deterministic.

```sh
cd Rust/guitar-web-wasm-demo/tools/damping-fit
cargo test --release                            # stability bound + noise-floor truncation
cargo run --release -- grid 1.0                 # decay sweep: train / held-out / all 10 notes
cargo run --release -- fit [--seed S] [--swap]  # CMA-ES on one fold, scored on the other
cargo run --release -- eval 0.987 1.0           # reference and render slopes, per-band error
cargo run --release -- sustain 0.987 1.0        # loop-gain bound + open-string T60
cargo run --release -- phrase ../../playwright-downloads/phrase.wav 0.987 1.0
```

## How it measures

- **Target notes.** The 10 cleanest single-note runs in the recording, from IX's
  `analyze_reference`. They are split by passage into 5 train and 5 held-out
  notes.
- **Slope.** Per-band decay slope in log-energy/s, over bands
  60-180-360-750-1600-3500-8000 Hz, measured from the RMS peak.
- **Noise floor.** A real performance never reaches silence, so each band's
  floor is the 5th percentile of frame energy over the whole recording.
  - A band is only fitted while it stays 10 dB above that floor (truncation).
  - A (note, band) pair is dropped if it gives less than 0.1 s above the floor,
    falls by less than 3 dB, or has a slope ≥ 0.
  - The render is measured over exactly the same span as the reference.
- **Error.** For each band, the error is the median absolute slope error over
  the surviving notes. A band needs at least 2 surviving notes. The loss is the
  mean over the bands that are scored.
- **Stability.** `is_stable` bounds the loop gain, `(decay + 0.0025·(1-f_norm))·sustain`, at
  every pitch from 82 to 1319 Hz. The fit rejects any `decay` whose bound
  reaches 1 - 1e-4. Tests check the bound against real renders.
- **Critic.** `phrase` renders the note sequence that `scripts/record-and-analyze.js` plays. Point
  `playwright-downloads/iteration-report.json` at it
  (`{"wav_path": "<abs path>"}`), then run `node scripts/run-spectral-critic.js`.
  The critic compares global spectra over the first 4 s. It does **not** measure decay.

## Result (2026-09-14)

Mean error over scored bands (log-energy/s):

| params (decay / brightness) | train | held-out | JS critic |
|---|---|---|---|
| 0.9978 / 0.80 (old default) | 2.582 | 2.693 | 0.6498 |
| 0.9978 / 1.0 (brightness only) | 2.614 | 2.148 | 0.6657 |
| 0.987 / 0.80 (decay only) | 3.932 | 3.501 | 0.6489 at 0.986 |
| **0.987 / 1.0 (shipped)** | **1.260** | **0.816** | **0.6797** |

- **The two settings only help together.** Brightness 1.0 alone recovers 29% of
  the held-out gain. Decay alone makes it worse.
- **Brightness-only is not an option.** With brightness 1.0, the old decay has
  an E2 loop gain bound of 1.000096 and a T60 of about 398 s.
- **Brightness 1.0 means the LP path is off.** Any base brightness ≥ ~0.85 saturates the per-voice
  clamp, which bypasses the dark `lp_alpha = 0.05` mix. 1.0 states that plainly.
- **Decay comes from a grid, not CMA-ES.** With brightness saturated the problem
  is 1-D, and a grid (`grid 1.0`) is enough:
  - The train and held-out folds both have their minimum at 0.986.
  - The union of all 10 notes is lowest at 0.986 (1.019), with 0.987 next (1.038).
  - Changing the floor settings moves the minimum to 0.987: margin 6 dB,
    or percentile 1% (0.987 to 0.988).
  - 0.987 ships as the value that is robust across those settings.
  - With a 15 dB margin, too few bands survive to be meaningful.
  - CMA-ES (`fit`) agrees: 0.986-0.988 on both folds and several seeds. It
    earns its place when the engine exposes a multi-parameter damping filter.

## Caveats

- **Early decay, one register.** 8 of the 10 notes are the same ~97 Hz pitch,
  and each window covers only the first 0.35-0.8 s. Using the fit for
  multi-second T60 assumes a single decay rate.
- **Upper strings are extrapolated.** Open-string T60 at 0.987 / 1.0 is
  E2 7.6, A2 5.6, D3 3.7, G3 2.5, B3 1.8, and **E4 1.1 s (unverified)**.
  At the old default it was E2 9.3, A2 121, D3 31, G3 10, B3 5.8, E4 2.7 s:
  - E2's loop gain bound was 1.000096, so it could not decay reliably.
  - A2 and D3 sat just below 1.0 (0.99982 and 0.99908) and decayed very slowly.
- **3.5-8 kHz.** Once the noise floor is handled, the residual is about 1.9-2.1,
  down from the 4.3 reported earlier, and it rests on 2-3 notes. The render still
  decays faster than the reference in 4 of 5 notes. That is weak evidence for a
  tunable loop filter, not proof that one is needed.
- **Most bands are dropped for most notes,** because the recording is dense. The
  error rests on few (note, band) pairs.
- **The UI slider maximum (0.9999) can make low strings unstable.** This was
  already true before this change, and so were profiles 1-3 (decay 0.9982,
  0.9985, 0.9987 all put the E2 bound above 1.0). Their LP mix dampens them in
  practice. None of this is changed here.

## En français

`damping-fit` ajuste `decay` et `brightness` du profil 0 pour que la décroissance
par bande d'une note rendue suive `by-the-lake.wav`.

- **Plancher de bruit.** La mesure coupe chaque bande à 10 dB au-dessus du
  plancher de bruit de l'enregistrement, et écarte les pentes non décroissantes.
- **Stabilité.** Toute valeur dont le gain de boucle atteint 1 est rejetée.
- **Réglages livrés.** decay 0,987 et brightness 1,0. Sur les notes de
  validation, l'erreur passe de 2,693 à 0,816. Le critique JS passe de 0,650 à
  0,680.
- **Les deux paramètres sont nécessaires ensemble.** Aucun des deux ne suffit
  seul.
- **Limites.** Il n'y a qu'un registre (~97 Hz). La mesure ne couvre que la
  décroissance initiale. Le T60 de la corde de mi aigu (1,1 s) n'est pas vérifié.
