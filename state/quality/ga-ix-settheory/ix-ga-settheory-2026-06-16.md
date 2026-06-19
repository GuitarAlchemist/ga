# IX ⟷ GA set-theory cross-check — 2026-06-16

Corpus: `ga-settheory-2026-06-16.json` · extension: `ix.duckdb_extension` · engine: DuckDB + IX UDFs.

Two independent set-theory implementations — GA's C# `SetClass` engine and IX's
Rust `ix-bracelet` (via `ix_icv` / `ix_prime_form` / `ix_forte_number`).
The **interval-class vector is convention-free**, so `ix_icv` MUST equal GA's
ICV for every set class — any mismatch is a real bug. Prime-form / Forte
differences are **informational** (Rahn vs Forte 1973 convention — the gap PR
#414 addressed).

Sidecars: `settheory-icv-disagreements.json` (should be empty),
`settheory-primeform-differences.json`.
|                                 section                                  |
|--------------------------------------------------------------------------|
| ## ICV cross-check — GA engine vs ix_icv (convention-free; MUST be 100%) |
| n_set_classes | icv_agree | icv_disagree | agree_pct |
|--------------:|----------:|-------------:|----------:|
| 222           | 222       | 0            | 100.0     |
|                         section                         |
|---------------------------------------------------------|
| ## ICV disagreements (each is a real bug in one engine) |
| card | ga_prime_str | ga_icv | ix_icv | ix_icv_str |
|-----:|--------------|--------|--------|------------|
|                              section                               |
|--------------------------------------------------------------------|
| ## Prime-form agreement (informational — Rahn vs Forte convention) |
|  n  | prime_agree | prime_agree_pct | ix_forte_unclassified |
|----:|------------:|----------------:|----------------------:|
| 222 | 181         | 81.53           | 0                     |
|                          section                          |
|-----------------------------------------------------------|
| ## Prime-form differences (sample — convention, not bugs) |
| card | ga_prime_str  |  ix_prime_str  | ix_forte |
|-----:|---------------|----------------|----------|
| 4    | [0,2,3,5]     | [0,1,3,10]     | 4-10     |
| 4    | [0,2,3,6]     | [0,1,3,9]      | 4-12     |
| 4    | [0,2,3,7]     | [0,1,3,8]      | 4-14     |
| 4    | [0,3,4,7]     | [0,1,4,9]      | 4-17     |
| 4    | [0,3,5,8]     | [0,2,5,9]      | 4-26     |
| 5    | [0,2,4,5,8]   | [0,1,3,5,9]    | 5-26     |
| 5    | [0,1,5,6,8]   | [0,1,3,7,8]    | 5-20     |
| 5    | [0,2,3,4,6]   | [0,1,2,4,10]   | 5-8      |
| 5    | [0,2,3,4,7]   | [0,1,2,4,9]    | 5-11     |
| 5    | [0,2,3,5,7]   | [0,1,3,5,10]   | 5-23     |
| 5    | [0,2,3,5,8]   | [0,1,3,6,10]   | 5-25     |
| 5    | [0,2,3,6,8]   | [0,1,3,7,9]    | 5-28     |
| 5    | [0,1,4,5,7]   | [0,1,3,8,9]    | 5-Z18    |
| 5    | [0,3,4,5,8]   | [0,1,2,5,9]    | 5-Z37    |
| 6    | [0,1,3,4,5,7] | [0,1,2,4,5,10] | 6-Z10    |
| 6    | [0,1,3,4,5,8] | [0,1,2,4,5,9]  | 6-14     |
| 6    | [0,1,4,5,6,8] | [0,1,2,4,8,9]  | 6-16     |
| 6    | [0,1,4,5,7,9] | [0,1,3,5,8,9]  | 6-31     |
| 6    | [0,1,4,6,7,9] | [0,1,3,6,7,10] | 6-Z50    |
| 6    | [0,2,3,4,5,7] | [0,1,2,3,5,10] | 6-8      |
| 6    | [0,2,3,4,5,8] | [0,1,2,3,5,9]  | 6-Z39    |
| 6    | [0,2,3,4,6,8] | [0,1,2,4,6,10] | 6-21     |
| 6    | [0,2,3,4,6,9] | [0,1,2,4,7,10] | 6-Z45    |
| 6    | [0,2,3,5,6,8] | [0,1,3,4,6,10] | 6-Z23    |
| 6    | [0,2,3,5,7,9] | [0,1,3,5,7,10] | 6-33     |

