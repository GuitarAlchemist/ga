# OPTIC-K retrieval-quality lens — 2026-06-16

Index: `optick.index` · per-instrument sample: 1000 ·
extension: `ix.duckdb_extension` · engine: DuckDB + IX UDFs.

**ix_kdist** (mean distance to k=5 nearest neighbours) is a local-density / OOD
signal — high kdist = an isolated voicing (rare, or a suspect embedding worth
inspecting). **ix_silhouette by instrument** measures how separable
guitar/bass/ukulele are: per OPTIC-K v1.8 the structure is instrument-INVARIANT
(same PC-set across instruments should be close), so **low/near-zero silhouette
is the GOOD, expected outcome** — a high positive value would mean instrument
identity leaked into the embedding.

Sidecars: `optick-isolated-voicings.json` (top-200 outliers),
`optick-per-instrument.json`.
|  section  |
|-----------|
| ## Sample |
| sampled | instruments | dim |
|--------:|------------:|----:|
| 3000    | 3           | 124 |
|                                 section                                  |
|--------------------------------------------------------------------------|
| ## Local density (ix_kdist, k=5) — distribution (higher = more isolated) |
|  min   | median |  p95   |  max   |  mean  |
|-------:|-------:|-------:|-------:|-------:|
| 0.0726 | 0.3648 | 0.5408 | 0.7084 | 0.3609 |
|                   section                    |
|----------------------------------------------|
| ## Per-instrument local density (mean kdist) |
| instrument |  n   | mean_kdist | max_kdist |
|------------|-----:|-----------:|----------:|
| bass       | 1000 | 0.3741     | 0.6702    |
| ukulele    | 1000 | 0.3642     | 0.6948    |
| guitar     | 1000 | 0.3444     | 0.7084    |
|                             section                             |
|-----------------------------------------------------------------|
| ## Most isolated voicings (highest kdist — inspect as outliers) |
| voicing | instrument | kdist  |
|--------:|------------|-------:|
| 324     | guitar     | 0.7084 |
| 306637  | ukulele    | 0.6948 |
| 306620  | ukulele    | 0.6926 |
| 306609  | ukulele    | 0.6901 |
| 712     | guitar     | 0.6886 |
| 306680  | ukulele    | 0.674  |
| 298123  | bass       | 0.6702 |
| 306596  | ukulele    | 0.6701 |
| 305723  | ukulele    | 0.6668 |
| 289     | guitar     | 0.6617 |
| 306631  | ukulele    | 0.6593 |
| 306686  | ukulele    | 0.6542 |
| 298319  | bass       | 0.6453 |
| 306512  | ukulele    | 0.6451 |
| 517     | guitar     | 0.6445 |
|                                               section                                               |
|-----------------------------------------------------------------------------------------------------|
| ## Instrument separability (ix_silhouette) — low/neg = instrument-invariant (GOOD per OPTIC-K v1.8) |
| overall_silhouette |
|-------------------:|
| 0.0197             |
| instrument | mean_silhouette |  n   |
|------------|----------------:|-----:|
| guitar     | 0.0257          | 1000 |
| bass       | 0.0167          | 1000 |
| ukulele    | 0.0167          | 1000 |

