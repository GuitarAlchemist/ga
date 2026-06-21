---
module: GA.Domain.Core.Theory.Atonal / GA.Business.ML.Embeddings
tags: [optic-k, duckdb, ix, invariants, set-theory, intervalclassvector, setclass, structure, transposition-invariance, doc-drift]
problem_type: architectural
decision: "Build a DuckDB exhaustive-invariant lens over GA's finite domain universes; use it to validate the domain, fix two real defects, and correct overstated OPTIC-K docs — but NOT change the embedding."
rejected:
  - "Re-encode IntervalClassVectorId to base-13 to make the chromatic aggregate lossless (1-of-4096 degenerate input; a one-way-door re-index)."
  - "Remove the pitch-class chroma from STRUCTURE to make it truly transposition-invariant (changes retrieval semantics; coordinated re-index)."
  - "Wire the ICV into the query path now (correctly deferred — would skew top-K against the misparsed corpus until a rebuild)."
reason: "Finite, total domain universes make invariants PROVABLE, not sampled — so a sweep distinguishes real bugs from doc/code drift. Two findings were intended design; the only systemic defect was overstated documentation. Embedding changes are one-way doors requiring owner sign-off."
date_decided: 2026-06-19
---

# DuckDB invariant sweeps find doc/code drift in finite domains — and OPTIC-K STRUCTURE is NOT transposition-invariant

## The method (reusable)

GA's domain types are **finite, enumerable, total universes**: all 4096 pitch-class sets,
the 224 Forte set classes, every key/mode. Unlike the chatbot/voicing data the `ix-duck`
lenses *sample*, these can be **exhaustively** materialised, so structural invariants are
**proven for every element**, not estimated.

Pattern (Tier 1, no embeddings): a C# exporter dumps a domain universe to JSONL (`Items` →
rows of id/cardinality/ICV/prime-form/round-trip), and a DuckDB SQL file checks invariants;
**an empty result per query = the law holds across the whole universe.** Tier 2 adds the
real layer-4 embedding code + cosine to test embedding-level invariants.

- Tools: `Tools/GaDomainInvariants` (Tier 1, layer-1 only), `Tools/GaStructureInvariance`
  (Tier 2, calls the real `TheoryVectorService`).
- Lens dir: `state/quality/domain-invariants/` (`build-invariants.sql`,
  `build-structure-invariance.sql`, README). JSONL is gitignored (regenerable).
- Run: `dotnet run --project Tools/GaDomainInvariants -c Release -- state/quality/domain-invariants`
  then `cd state/quality/domain-invariants ; duckdb < build-invariants.sql`.

## What it found (three things, two of which were NOT bugs)

1. **Real bug — `IntervalClassVector.Major` (PR #432).** The id encoding is base-12, but
   `Major` was the literal `254361` (a stale base-10 mnemonic), which decodes to the nonsense
   `<1 0 3 2 4 9>` instead of `<2 5 4 3 6 1>` (base-12 id 608761). Unreferenced in logic, so
   nothing live broke — a latent trap. Fix: derive `Major` from its counts. The sweep also
   proved the base-12 packing is lossy for **exactly 1** of 4096 sets (the chromatic
   aggregate, count=12); documented, not re-encoded.

2. **Real improvement — `SetClass.ToString()` ambiguity (PR #433).** Cardinality + ICV id are
   not unique: the **23** Z-related pairs (the textbook 12-TET count — itself a validation
   that `SetClass`/`PrimeForm` are correct) share both, so 46 set classes rendered to 23
   identical strings. `Equals`/`GetHashCode` already keyed on `PrimeForm`; the label now does
   too. Exhaustive 224-class regression test.

3. **NOT a bug — STRUCTURE is not transposition-invariant (doc error, PR #433).** The Tier-2
   sweep measured every set class × 12 transpositions: **221 of 222 are not T-invariant**
   (mean min-cosine 0.88, worst 0.51; a major triad is 0.66–0.78 vs its transpositions). Root
   cause: STRUCTURE carries a 12-dim pitch-class **chroma** (`TheoryVectorService` `v[pc]=1.0`,
   dims 6–17) that is transposition-variant. **This is intended** (the chroma provides
   same-PC-set matching, which invariant #25 actually tests). The defect was the docs.

## The durable OPTIC-K clarification

STRUCTURE (dims 6–29, weight 0.45) is invariant for **re-voicings of the same pitch-class
set** — octave / voicing / instrument (invariants #25, #32). It is **NOT** transposition- or
inversion-invariant. Only the **ICV + cardinality sub-dims** are T/I-invariant; the chroma
sub-dims are not. The v1.8 ROOT split fixed *same-PC-set* invariance (root varied across
instruments for a fixed PC set), which people mislabelled "T-invariance."

Consequences verified by investigation (no retrieval bug):
- Search (`OptickSearchStrategy.ApplyFilters`) has a root-PC filter that **deliberately keeps
  transpositions apart** — transposed-shape retrieval is unsupported *by design*.
- The one "similar chords" feature (`IcvNeighborsSkill`) bypasses the embedding and uses
  T-invariant **ICV L1 distance** (Grothendieck) — the correct path for cross-key shape.
- The ICV is currently zero on the query side (`MusicalQueryEncoder` line 52) — a *documented,
  deliberate* deferral gated on the corpus rebuild (`docs/plans/2026-05-12-icv-format-
  reconciliation-plan.md §2`), guarded by `OptickIntegrationTests.cs:118`. Not a bug.

Corrected docs: `CLAUDE.md` (+ synced `AGENTS.md`), `EmbeddingSchema.cs` (ROOT + v4-pp-r
comments), `OPTIC-K_Applications_Guide.md`, `TheoryVectorService.cs` inline comment.

## Lessons / how to apply

- **Finite domain → exhaustive invariant SQL.** Round-trip (`decode(encode(x))==x`),
  conservation laws (ICV sum == C(n,2)), uniqueness (`GROUP BY … HAVING count>1`),
  cross-impl diffs (C# vs Rust). One-off bugs become standing guards.
- **Ask "is this *really* a bug?" before fixing.** Two of three findings were intended design
  + a doc error. The sweep is for *evidence*; the disposition is judgement. Don't touch
  one-way doors (embedding dims, encoding base) speculatively — surface + let the owner decide.
- **Docs drift silently; a sweep is a doc-accuracy test.** "STRUCTURE is T-invariant" propagated
  into four docs *and a teaching course* before measurement caught it. The same trick that
  found `Major` (reconcile a doc claim against the code) generalises.
- **Provenance:** this arc began as a `/teach` domain-model course — explaining the model out
  loud was the forcing function. See PRs #432, #433.
