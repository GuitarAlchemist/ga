# Chatbot voicing queries: a NullReferenceException, not a routing/LLM problem

**Date:** 2026-05-30 (corrected after live verification + an octo 4-way debate)
**Status:** root cause confirmed by live stack trace; NRE fixed (PR `fix/voicing-search-null-filter-fields`)

> **This note previously claimed "routing is embedding-first and there is no
> deterministic CanHandle pre-pass." THAT WAS WRONG.** Live verification proved
> a deterministic pre-route exists and fires correctly, and the actual voicing
> failure is a null-deref. Corrected below so the next person doesn't chase the
> wrong layer (as I did for several iterations).

## What actually breaks voicing queries

"Dmaj7 voicing on guitar" (and every "<chord> voicing/shape/fingering" query)
returned a wrong LLM "fallback-direct" answer. The cause is a single bug:

`Common/GA.Business.ML/Search/CpuVoicingSearchStrategy.cs:184` (`MatchesFilters`)
dereferenced `voicing.VoicingType` with no null check (unlike the `StackingType`
guard 13 lines below). When a query produced a `VoicingType` filter and ANY
corpus voicing had a null `VoicingType`, `.Contains` threw a
`NullReferenceException` **inside a `Parallel.ForEach`** → the whole voicing
search threw → `ProductionOrchestrator` caught it → `error-fallback` to the LLM.

Live stack trace (2026-05-30):
```
VoicingAgent processing: Dmaj7 voicing on guitar      ← the deterministic guard DID fire
System.NullReferenceException
  at CpuVoicingSearchStrategy.MatchesFilters     line 184
  at CpuVoicingSearchStrategy.HybridSearchAsync  line 128  (Parallel.ForEach)
  at VoicingAgent.ProcessAsync                   line 83
  at ProductionOrchestrator.DispatchDeterministicAgentAsync line 619
→ "Chat orchestration failed. Falling back to direct chat client."
```

## What is actually TRUE about routing (corrected)

- **A deterministic voicing pre-route EXISTS and fires before the embedding
  router.** `ProductionOrchestrator.TrySelectDeterministicAgent` (line 739) +
  `IsExplicitVoicingRequest` (775) + `ExplicitVoicingKeywords` (126), called at
  3 sites (214/350/490). `IsExplicitVoicingRequest("Dmaj7 voicing on guitar")`
  is true; `VoicingAgent.AgentId == AgentIds.Voicing` ("voicing") matches. The
  guard worked — it dispatched to `VoicingAgent`, which then threw.
- Embeddings are fast (nomic-embed ~0.1s) — never the bottleneck.
- The LLM path (`llama3.2:3b`, 6–15s) IS genuinely slow, but that only bites
  queries with **no** matching deterministic skill (e.g. "notes in A minor",
  "ii-V-I in Bb"). It was NOT the voicing failure.

## Fix shipped

Null-guard `VoicingType` + the sibling unguarded fields (`Difficulty`,
`Position`, `ChordName`, `PrimeFormId`, `SemanticTags`) with the same
"null ⇒ does not match" convention `StackingType` already uses. Verified live:
**0 NullReferenceExceptions** after the fix (was thrown on every voicing query).

## Honest limitation — what the fix does NOT do

Stopping the crash does not by itself make voicing queries return voicings. With
the NRE gone, the search now cleanly returns **0 results** → `no-match-fallback`,
because of the **pre-documented corpus-tagging-mismatch**
(`docs/solutions/architecture/2026-05-08-voicing-search-corpus-tagging-mismatch.md`):
filters target fields the corpus doesn't populate. `ChordVoicingsSkill` works
around this with a retry-without-filters; **`VoicingAgent` (the deterministic
path) does not.** Giving `VoicingAgent` the same retry is the next step to make
voicing queries actually return results.

## Separate open items (not this bug)

- **"ii-V-I in Bb" → misroute**: no progression-generation skill exists; the
  semantic router has no Roman-numeral hint rule in `DefaultRoutingHintProvider`.
- **First-query startup race**: "Service not initialized. Call
  InitializeEmbeddingsAsync first." on the very first query after a cold start.

## Takeaway (process)

For chatbot failures, **read the live trace/stack first.** Several iterations
(including an autonomous central-orchestrator rewrite that was correctly
reverted) chased "embeddings slow" then "no deterministic pre-pass" — both
wrong. An octo 4-way debate forced reading the actual code (found the existing
pre-route), and the live stack trace then pinned a one-line null-deref. The
deterministic corpus is 100% but has no LLM-bound/voicing-search prompts, so it
never caught this.

---

**See also:** [2026-07-20-router-anchor-shape-misroute-chord-vs-scale.md](2026-07-20-router-anchor-shape-misroute-chord-vs-scale.md)
— same subsystem, different root cause (ExamplePrompt anchor shape, not a
null-deref), and an independent instance of the closing observation above: the
corpus was green because it had no prompt of the failing *shape*.
