# GA domain-invariants lens (DuckDB ⨯ IX, Tier 1)

Exhaustive structural-invariant sweep over GA's **finite, enumerable** domain universes —
all 4096 pitch-class sets and the full 224-entry Forte set-class catalog. Unlike the
chatbot/voicing lenses that *sample* real data, these domains are total, so invariants are
**proven for every element**, not estimated.

Pattern borrowed from `ix-duck`'s lens architecture (see `../../../../ix/learning/duckdb-ix`).

## Run
```powershell
# 1. export the universes from the C# domain (layer-1 -> JSONL)
dotnet run --project Tools/GaDomainInvariants -c Release -- state/quality/domain-invariants
# 2. sweep the invariants (empty result per check = the law holds for the whole universe)
cd state/quality/domain-invariants ; duckdb < build-invariants.sql
```

## Invariants checked
| id | law | result |
|----|-----|--------|
| I1 | ICV counts sum to C(n,2) | holds for 4095/4096; the **single** violation is the chromatic aggregate (count-12 base-12 packing limit — see `IntervalClassVectorId`) |
| I2 | base-12 re-encode of ICV reproduces its id | same single aggregate case |
| I3 | every ICV count fits a base-12 digit (0–11) | holds |
| I4 | a set and its prime form share an ICV (T+I invariance) | **holds exhaustively** (0 violations) |
| I5 | distinct prime forms == set-class count | 224 == 224 ✓ |
| D  | Z-related set classes (same ICV, different prime form) | **23 pairs** — the textbook 12-TET count; independently confirms the catalog is correct |

## Findings it produced
- **Validated** the `SetClass`/`PrimeForm` implementation: exactly 23 Z-related pairs at
  cardinalities 4/5/6/7/8 — the canonical number. The domain reproduces the textbook catalog.
- **Improved C# (PR):** `SetClass.ToString()` was ambiguous — Z-related pairs share
  cardinality + ICV id, so 46 distinct set classes rendered to 23 identical strings. Now
  includes the prime-form id (`Equals` already keyed on it). Regression test:
  `Tests/.../Atonal/SetClassToStringTests.cs`.
- **Confirmed** the `IntervalClassVector` base-12 limitation affects exactly 1 of 4096 sets.

## Tier 2 — STRUCTURE transposition-invariance (BUILT)
`Tools/GaStructureInvariance` runs the real `TheoryVectorService.ComputeEmbedding` over every
set class at all 12 transpositions; `build-structure-invariance.sql` measures
`list_cosine_similarity(t0, tk)`.

**Finding (significant):** STRUCTURE is **NOT transposition-invariant**, contradicting the
inline claim in `TheoryVectorService`, CLAUDE.md's OPTIC-K section, and the teaching course.
- 1 of 222 set classes is T-invariant (only the chromatic aggregate); **221 are not**.
- mean min-cosine across transpositions **0.88**, worst **0.51**; a major triad sits at
  **0.66–0.78** vs its transpositions (1.0 would mean invariant).
- Root cause: the pitch-class **chroma** (`v[pc]=1.0`, dims 6–17 — half of STRUCTURE) encodes
  the literal pitch classes. The v1.8 ROOT split fixed *same-pitch-class-set* invariance
  (invariant #25), which is what the chroma provides; it never made STRUCTURE *transposition*-
  invariant.

**Disposition:** corrected the false code comment + flagged the docs. Did NOT change the
embedding — whether the chroma belongs in STRUCTURE (same-PC matching, intended) or should
move to its own partition for true T-invariant *shape* retrieval is a one-way-door design
call for the schema owner. T/I-invariant signal already exists in the ICV + cardinality dims.

## Next tiers (not yet built)
- silhouette/pca vs the Forte taxonomy; C#↔Rust ICV diff over the full universe; loading the
  `ix.duckdb_extension` (`ix_cosine`/`ix_euclidean`) in place of the built-in `list_cosine_similarity`.
