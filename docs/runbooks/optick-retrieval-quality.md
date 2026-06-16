# OPTIC-K retrieval-quality lens

Measures the health of the OPTIC-K voicing index (`state/voicings/optick.index`)
with IX's DuckDB UDFs — local-density / outlier detection (`ix_kdist`) and
instrument-invariance (`ix_silhouette`). No Parquet export: `ix_optick_scan`
reads the index mmap directly.

## Why

The index is the substrate for voicing retrieval (RAG, `ga_search_voicings`,
neighbour suggestions). Two things make it "good":

- **No isolated embeddings.** A voicing whose nearest neighbours are all far
  away (`ix_kdist` high) is either genuinely rare or a suspect embedding — worth
  inspecting. A long high tail would mean retrieval returns junk for those
  queries.
- **Instrument-invariant structure.** Per OPTIC-K v1.8 the embedding is designed
  so the SAME pitch-class set across guitar/bass/ukulele lands CLOSE (the ROOT
  partition closed the T-invariance gap invariant test #25 exposed). So
  guitar/bass/ukulele should NOT separate into clusters — **low/near-zero
  instrument silhouette is the GOOD outcome**. A high positive value would mean
  instrument identity leaked into the embedding (a regression).

## Run

```powershell
# duckdb on PATH + ix.duckdb_extension built. The index is gitignored, so a
# fresh worktree has no copy — the runner falls back to the primary worktree's,
# or pass -Index / set GA_OPTICK_INDEX.
pwsh Scripts/optick-retrieval-quality.ps1
pwsh Scripts/optick-retrieval-quality.ps1 -PerInstrument 2000 -Index D:\path\optick.index
```

Sampling is stratified by instrument (`-PerInstrument`, default 1000 → ~3k
total) to keep the O(n²) kdist/silhouette tractable and to represent bass/ukulele
(tiny vs guitar's ~298k).

## Outputs (committed as the record)

| File | What |
|---|---|
| `optick-retrieval-<date>.md` | kdist distribution, per-instrument density, top isolated voicings, instrument silhouette |
| `optick-isolated-voicings.json` | top-200 outlier voicing ids by kdist (inspect list) |
| `optick-per-instrument.json` | per-instrument mean kdist + silhouette |

## Baseline (2026-06-16, 3k sample)

- kdist k=5: median 0.365, p95 0.541, max 0.708 — tight, no extreme outliers.
- Per-instrument mean kdist: bass 0.374 · ukulele 0.364 · guitar 0.344 — even.
- **Instrument silhouette overall 0.0197** — near zero, **confirming the v1.8
  instrument-invariance design goal empirically**.
- Top outliers to inspect: guitar 324 (0.71), ukulele 306637/306620/306609 (~0.69).

## Reading it

- **Rising overall/p95 kdist** across runs → the index is getting sparser /
  noisier (an embedding regression or a corpus-coverage gap).
- **Instrument silhouette climbing well above ~0.05** → instrument leakage; a
  one-way-door OPTIC-K change may have broken cross-instrument invariance —
  cross-check the invariant suite (`ix-optick-invariants`).
- **A voicing recurring at the top of the outlier list** → inspect its diagram /
  midi notes; either a legitimately rare shape or a bad embedding.
