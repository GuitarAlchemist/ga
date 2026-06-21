---
title: "\"GPU filter reconciliation\" was reconciling the wrong pair — production runs neither CPU nor GPU"
date: 2026-06-20
module: GA.Business.ML.Search
component: "IVoicingSearchStrategy — Cpu / Gpu / Optick + VoicingFilterEngine + VoicingComfortFilter"
problem_type: architectural
decision: "Scope CPU↔GPU parity to the metadata *predicate*; keep OPTK's filter set index-bound + observable (telemetry); lift comfort to a shared seam applied by all three strategies."
rejected:
  - "Fork VoicingFilterEngine's null-bias (null→pass) so OPTK can cross it — gives the one canonical predicate two contradictory null semantics"
  - "Claim full result-set parity between CPU and GPU — false under score-ordered truncation"
  - "Reindex OPTK now to carry rich metadata — one-way door, deferred + demand-gated (#448)"
reason: "Production selects OPTK whenever the index file exists, so it runs neither CPU nor GPU. The real risk was OPTK silently ignoring ~30 filters and comfort being GPU-only, not CPU↔GPU drift. Predicate parity is the only sound, non-circular invariant; OPTK's reduced set is data-bound, not a bug."
date_decided: 2026-06-20
symptoms:
  - "A refactor branch named `refactor/gpu-filter-reconciliation` set out to make the CPU and GPU voicing-search strategies 'not disagree' on filtered results"
  - "GPU strategy comment claimed 'the two paths can't disagree' — but the same filtered query could return different results depending on the active strategy"
  - "Comfort/ergonomic filters (MinComfortScore, MustBeErgonomic) silently ignored on the CPU fallback and on the production-default OPTK path; only GPU applied them"
  - "Comfort filtering silently no-op'd even on GPU for compact `x35453` diagrams (parser split on '-' only → 0 positions → lenient pass)"
  - "OPTK (the production default) honored only ChordName + MIDI-range + instrument and silently dropped ~30 other filters with no signal"
tags: [voicing-search, optic-k, parity, ilgpu, filter-engine, comfort-filter, strategy-pattern, predicate-vs-result-set, adversarial-review, cross-strategy-drift]
---

# Voicing search-strategy filter parity — reconcile the pair that actually runs

## Symptom

A refactor extracted a shared `VoicingFilterEngine` and routed the CPU and GPU voicing-search
strategies through it so they'd "stop disagreeing." The GPU adapter's comment asserted *"the two paths
can't disagree."* On the surface this looked done. It wasn't — and the framing was off-target.

## Investigation (what the surface story missed)

1. **Which strategy actually runs in production?** `IVoicingSearchStrategy` has **three**
   implementations, not two. Both GaApi and GaChatbot.Api register **`OptickSearchStrategy` (OPTK-mmap)
   as the default whenever the index file exists** — which it normally does. CPU and GPU are
   fallback / explicit-opt-in only. So the entire "CPU vs GPU" reconciliation was about two strategies
   **that production almost never selects.**

2. **OPTK had its own, third, drifted filter path.** `OptickSearchStrategy.ApplyFilters` honored only
   `ChordName`, `MinMidiPitch`, `MaxMidiPitch` (+ `VoicingType` as an *instrument* route). It silently
   ignored ~30 other filters — because the OPTK mmap index is **metadata-thin** (it carries only
   diagram, inferred quality, instrument, MIDI notes; `SemanticTags=[]`, no `Difficulty`/`StackingType`/
   Phase-3-4 fields). This is **data-bound, not a code bug.**

3. **Comfort was GPU-only by accident.** `MinComfortScore`/`MustBeErgonomic` ran only in
   `GpuVoicingSearchStrategy.ApplyComfortFilters` — but that code uses `BiomechanicalAnalyzer`, which is
   **pure C# over the diagram** and needs no GPU. CPU and OPTK (both of which carry the diagram) just
   never called it. So the production paths silently skipped a filter callers explicitly asked for.

4. **The parity claim overclaimed (adversarial review caught this).** Even for CPU vs GPU, "admit the
   same *set*" is only true for the **pre-truncation candidate set**. The strategies *score* differently
   (CPU `TextEmbedding ?? Embedding` + symbolic boosting; GPU musical `Embedding`), so a score-ordered
   `Take(limit)` returns **different top-K subsets once survivors exceed `limit`** (production `topK=10`).
   "Same surviving set" was wrong; "same predicate" was right.

5. **A latent throw in the lifted parser.** `ParseDiagramToPositions` called `new Fret(value)` with no
   guard; a dash-format fret outside `[-1,36]` throws `ArgumentOutOfRangeException` — inside the search's
   `Parallel.ForEach` / kernel filter, which faults the **entire query** (the same
   collapse-the-whole-search class as the 2026-05-30 null-safety fix).

## Root cause

The work was named and scoped around the wrong axis. "Reconcile CPU and GPU" optimized a divergence that
production rarely hits, while the divergence production *actually* serves — OPTK's silent filter-dropping
and comfort being unreachable off the GPU path — went untouched. Compounding it, the parity *invariant*
itself was imprecise (predicate vs result-set), so the code/docs asserted a guarantee that doesn't hold.

## Solution

- **Shared `VoicingComfortFilter`** (sibling to `VoicingFilterEngine`) applied by **CPU + GPU + OPTK**.
  Comfort never needed the GPU; the diagram is in every strategy's reach (`VoicingEmbedding.Diagram` /
  `OptickMetadata.Diagram`). Parser now handles **both** diagram formats and **range-guards the fret
  (0..24) before constructing `Fret`/`MidiNote`**, so it degrades to the lenient "keep" outcome instead
  of throwing.
- **`IVoicingSearchStrategy.UnsupportedPopulatedFilters`** — default empty (CPU/GPU honor every metadata
  field via the engine); OPTK overrides with its **index-bound drop-list**. Surfaced as `dropped`
  telemetry on `VoicingTelemetryRecord`, so OPTK's reduced set is **observable, not silent**.
- **Honest invariant.** Code, `CONTEXT.md`, and **ADR-0002** now say **predicate parity, not result-set
  parity**, with the truncation boundary stated. The CPU↔GPU parity test runs at `limit ≥ corpus` and
  asserts both strategies match an **independently computed** filter-pass set (not just each other).
- **Deferred the reindex** (the only path to true three-way parity) as a **demand-gated one-way door**
  (#448), scoped by the new `dropped` telemetry — don't reindex speculatively.

## Prevention (the compounding lessons)

1. **Before "reconciling" two implementations, confirm which one runs in production.** Count the
   `IVoicingSearchStrategy` registrations; the *default selection logic* (index-present → OPTK) is the
   load-bearing fact, not the number of classes. Reconciling non-default strategies is motion, not progress.
2. **Predicate parity ≠ result-set parity.** Any "implementations A and B return the same results" claim
   is false the moment they *score differently* **and** a `Take(limit)` truncates — survivors > limit cut
   at different rows. State which one you guarantee; test at `limit ≥ corpus` for predicate parity, and
   never assert result-set parity you can't hold.
3. **A predicate lifted into a hot loop must be total (never throw).** Range-guard before any throwing
   constructor; an exception inside `Parallel.ForEach`/a kernel filter collapses the whole query. Same
   class as the 2026-05-30 null-safety fix — see `VoicingFilterEngine`'s remarks.
4. **Make reduced capability observable, not silent.** A strategy that can't honor a filter should
   *declare* it (capability method → telemetry), so the gap becomes evidence (here: which fields to
   index, #448) instead of an invisible correctness hole.

## Related

- `docs/adr/0002-voicing-filter-parity-cpu-gpu-only.md` — the decision record
- `docs/solutions/architecture/2026-05-08-voicing-search-corpus-tagging-mismatch.md` — earlier OPTK metadata-thinness symptom
- `docs/solutions/architecture/2026-06-19-duckdb-invariant-sweep-and-optick-structure-t-invariance.md` — OPTK structure invariants
- PR #449 (shipped); follow-up #448 (OPTK index enrichment, demand-gated)
