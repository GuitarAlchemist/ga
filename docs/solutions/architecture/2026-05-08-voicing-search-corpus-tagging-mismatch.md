---
date: 2026-05-08
module: voicing-search
tags: [optic-k, voicing, search, corpus, filters, embedding, gpu]
problem_type: corpus-vocabulary-mismatch
status: open
---

# Voicing search returns "no matches" — corpus-tagging vocabulary mismatch + dual code paths

## Symptom

Every voicing prompt routed through the chatbot returns `"The OPTIC-K index returned no matches for this query."` despite the corpus holding 667,125 voicings.

```
"Drop 2 voicings of Cmaj7"   → no matches (instant)
"Rootless Dm7 voicings"      → no matches (instant)
"Show me chord shapes for F#m7" → no matches (instant)
"voicings of Am7"            → no matches (instant)
```

But the same corpus and same encoder respond correctly via a different endpoint:

```
POST /api/voicings/retrieve {"query":"Cmaj7", "limit":3}
  → 3 results at 0.55 cosine in 1.9s
```

## Root cause — two layers

The chat path and the `/retrieve` path are entirely different services with different filter contracts.

### Layer 1 — code path divergence

```
VoicingAgent.ProcessAsync
  └─ extractor.ExtractAsync(query)            → StructuredQuery {ChordSymbol, ModeName, Tags}
  └─ encoder.Encode(structured)               → 124-dim compact OPTIC-K query vector
  └─ BuildSearchFilters(structured)           → VoicingSearchFilters {ChordName, Tags, ...}
  └─ voicingSearch.SearchAsync(...)           → EnhancedVoicingSearchService
        └─ filters != null → HybridSearchAsync (GpuVoicingSearchStrategy)
                              └─ pre-filter by Tags (strict equality)
                              └─ then cosine
        └─ filters == null → SemanticSearchAsync (GpuVoicingSearchStrategy)
                              └─ cosine over all 667k

VoicingsController.Retrieve
  └─ semanticKnowledge.SearchAsync(query, limit)   → ISemanticKnowledgeSource
        └─ different impl entirely; fast (1.9s); no Tag/ChordName filters
```

The two services share the corpus but **not** the filter pipeline or the search strategy.

### Layer 2 — filter contract vs corpus vocabulary

`VoicingAgent.BuildSearchFilters` populates `Tags = ["drop2", "shell", "rootless"]` and `ChordName = "Cmaj7"` from the structured query. But:

| What the chat-path filter expects | What the corpus actually contains |
|---|---|
| `Document.ChordName` contains `"Cmaj7"` | `Document.ChordName = meta.QualityInferred ?? "Unknown"` — for the bulk of the corpus, this is literally `"Unknown"` |
| `Document.SemanticTags` contains `"drop2"` | `Document.SemanticTags = ["consonant", "quality", "advanced"]` — playing-characteristic vocabulary, not technique-name vocabulary |

`OptickSearchStrategy.ApplyFilters` does `d.ChordName.Contains(filters.ChordName)`. `"Unknown".Contains("Cmaj7")` → false. Filter rejects everything.

`GpuVoicingSearchStrategy.HybridSearchAsync` line 289 does `filters.Tags.Any(tag => v.SemanticTags.Contains(tag))`. `["drop2"].Any(t => ["consonant","advanced"].Contains(t))` → false. Filter rejects everything.

The vocabularies were chosen by independent components and never reconciled.

## What was tried, what didn't work

### Attempt 1 — fix `OptickSearchStrategy.ApplyFilters` (commit `fddff936`)

Codex CLI pinpointed the `ChordName.Contains` bug. The fix splits the chord symbol into root + quality and matches on quality fragment + root pitch class.

**Outcome**: correct in its own right. **Not applicable to the live demo** — GaApi enables CUDA at startup (`accelerator=Cuda realGpu=True`), which selects `GpuVoicingSearchStrategy`, not `OptickSearchStrategy`. The fix shipped but the live behavior didn't change.

The fix still pays off for any host running `OptickSearchStrategy` (CPU-only deployments, tests, the `Scripts/run-dsl-eval-soak.ps1` runner).

### Attempt 2 — drop `ChordName` filter from `VoicingAgent.BuildSearchFilters`

Reasoning: the encoder already encodes the chord through STRUCTURE + ROOT partitions; semantic similarity should rank Cmaj7 voicings near the top.

**Outcome**: didn't help. `Tags` filter still rejected everything because of the vocabulary mismatch on `Document.SemanticTags`.

### Attempt 3 — drop `ChordName` AND `Tags` filters

Reasoning: same as above — both are encoded into the query vector.

**Outcome**: filters become `null`, `EnhancedVoicingSearchService.SearchAsync` routes to `SemanticSearchAsync` instead of `HybridSearchAsync`. **Voicing search hangs** (>180s timeout on every prompt) — semantic search over 667k voicings without any pre-filter is slow on the chat-path's GPU strategy, even though it's fast on the `/retrieve` path's `ISemanticKnowledgeSource`. Reverted.

## Why `/retrieve` is fast and the chat path isn't

Both run cosine over the same corpus, but:

- `/retrieve` uses `ISemanticKnowledgeSource.SearchAsync(query, limit, ct)` — text-input API, presumably the implementation hits the GPU index in a single batched cosine pass with internal pruning.
- Chat path uses `EnhancedVoicingSearchService.SearchAsync(query, generator, limit, filters, ct)` with a precomputed query vector. Without filters, it performs a full-pool cosine without the `/retrieve` path's pruning, which scales poorly.

Empirically: `/retrieve` returns 1.9s, chat-path-no-filters > 180s. **Same corpus, different cost profile.**

## Three viable fixes (none shipped)

| # | Approach | Cost | Risk |
|---|---|---|---|
| 1 | **Map extractor tags → corpus tag vocabulary** at the filter boundary. E.g. `"drop2" → ["voicing-shape:drop2", "technique:drop2"]` if any of those exist in the corpus. Inspect 10 corpus rows, find the actual tag schema, write a mapper. | Hours | Tags need to genuinely exist in the corpus, otherwise mapping accomplishes nothing. |
| 2 | **Switch `VoicingAgent` to use `ISemanticKnowledgeSource`** (the `/retrieve` path's service) and pass the structured-query-derived prompt as text. Drops the encoder's structured pipeline; relies on text embedding. Loses partition-weighted cosine semantics. | Day or two | Loses the whole reason `MusicalQueryEncoder` exists. |
| 3 | **Make chat-path `SemanticSearchAsync` performant.** Investigate why `GpuVoicingSearchStrategy.SemanticSearchAsync` is >100× slower than `ISemanticKnowledgeSource`. Likely either an unbatched loop, a missing GPU pruning step, or a bad query-vector format. | Day, probably | Real diagnostic work. Closes the dual-code-path drift permanently. |

Path 3 is correct architecturally — the duality is the bug — but it's the most expensive. Path 1 is the smallest valuable cut if a corpus tag schema can be discovered.

## How to pick up

1. **Run the diagnostic**: `curl -X POST localhost:5232/api/voicings/retrieve -d '{"query":"Cmaj7","limit":10}'`. Inspect `results[*].snippet` for the actual `Tags:` line. That's the corpus's tag vocabulary; map extractor outputs into it.
2. **Audit the corpus schema**: check `voicings_v1.bin` indexing code for what fields are populated. The metadata-mapping in `OptickSearchStrategy.cs:202-223` shows `meta.QualityInferred` and `meta.Instrument` come through, but `SemanticTags = []` is hardcoded empty. Where do the corpus's actual tags come from? `GpuVoicingSearchStrategy.cs:808` uses `voicing.ChordName` — different mapping.
3. **One-shot script**: write a console probe that loads the corpus into `EnhancedVoicingSearchService` directly and inspects the first ten voicings' `SemanticTags`, `ChordName`, and `MidiNotes`. Confirm the vocabulary before changing any code.

## What this teaches

- **Two services backing the same corpus drift apart silently** when there's no contract test that exercises both with the same query and asserts equivalent results.
- **Filters and embeddings should not be redundant**. The encoder spent time encoding the chord into the vector; a hard filter on the same chord undoes that work and adds rejection paths.
- **Capability matrix smoke is load-bearing.** Without the 2026-05-08 capability run that surfaced "OPTIC-K index returned no matches", this would have stayed quietly broken on the public demo indefinitely.

## Related commits

- `fddff936` — `OptickSearchStrategy` filter fix (correct, not the live path).
- Codex CLI 2026-05-08 review (b9zstl2b5): pinpointed the `ChordName.Contains` bug; prescribed the "filters null vs filters populated" diagnostic.
- `Scripts/run-dsl-eval-soak.ps1` — soak runner; might surface this when run against a Cmaj7-shaped prompt.
