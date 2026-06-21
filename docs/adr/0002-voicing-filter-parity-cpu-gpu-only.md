# Voicing-filter parity is scoped to CPU↔GPU; the OPTK production path bypasses the shared filter engine

`VoicingFilterEngine` is the single metadata predicate the CPU and GPU strategies cross, so the
same voicings **pass the filter** for a given `VoicingSearchFilters`. This is **predicate parity,
not result-set parity**: the strategies *score* differently (CPU scores `TextEmbedding ?? Embedding`
and adds Phase-7 symbolic boosting; GPU scores the musical `Embedding` only), so a score-ordered
`Take(limit)` returns **different top-K subsets when the surviving candidate count exceeds `limit`**
(production `topK` is 10 over a large corpus). The guarantee is "the same candidates pass the
filter," *not* "the same voicings are returned." Reconciling the *scoring* of the two strategies is
a separate, larger question (different embedding spaces by design) and is explicitly out of scope
here. The production-default `OptickSearchStrategy` deliberately does **not** route
through it: the OPTK mmap index carries only diagram, inferred quality, instrument, and MIDI
notes, so honoring the ~30 rich filters would reject every row — its empties/nulls mean "not
indexed," the **opposite** of the engine's "absent attribute → reject" semantics. Optick
therefore keeps an index-bound filter set and declares what it cannot honor via
`IVoicingSearchStrategy` (surfaced as `dropped` telemetry) rather than silently dropping
filters or being forced into the engine with a forked null-bias.

Comfort/ergonomic filters (`MinComfortScore`, `MustBeErgonomic`) are a *separate* shared seam,
not part of metadata parity: they are diagram-derived via the biomechanical analyzer, carry the
**opposite (lenient) unknown-bias** (a diagram we can't parse passes rather than fails), and —
because every strategy carries the diagram — are applied by CPU, GPU, **and** Optick.

## Considered options

1. **Honest reduced set + observability — chosen (now).** Keep `VoicingFilterEngine` as the
   unambiguous CPU/GPU predicate; let Optick honor only its index-bound filters and make every
   strategy declare its unsupported-filter set so the gap is visible (telemetry), never silent.
2. **Fork the engine to treat null as "pass"** so Optick could cross it — **rejected**. It gives
   the one canonical predicate two contradictory null-biases (reject for the rich corpus, pass
   for the thin one), destroying the single virtue that motivated extracting the engine.
3. **Enrich the OPTK index** so Optick crosses the seam for real — **deferred**. This is a corpus
   reindex (OPTIC-K one-way door, requires sign-off). It is demand-gated; the `dropped` telemetry
   from option 1 collects the evidence to scope *which* fields are worth indexing when a real
   consumer appears.

## Consequences

- "Parity" claims in code/comments/tests must say **CPU↔GPU metadata *predicate***, not "all paths
  agree" and not "same returned set." The production default agrees with neither on the full filter
  set, by design.
- **Predicate parity ≠ result-set parity.** Because CPU and GPU score differently (and CPU adds
  symbolic boosting), the returned top-K diverges whenever surviving candidates exceed `limit`. The
  `CpuGpuFilterParityTests` tripwire therefore asserts the **filter-pass set** (run at `limit ≥ corpus`
  so truncation never bites) and is deliberately *not* a test of scoring, ranking, or truncation. It
  also cannot exercise the real ILGPU kernel on CPU-only CI (the GPU strategy falls back to a managed
  loop), so kernel-vs-host scoring divergence is out of its scope by construction.
- A future "route Optick through `VoicingFilterEngine`" change is a regression unless it is paired
  with option 3 (the reindex) — without richer index metadata it returns empty result sets.
