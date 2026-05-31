---
title: ChordVoicingsSkill High-Value Test Plan
target: Common/GA.Business.ML/Agents/Skills/ChordVoicingsSkill.cs
status: draft
generated_at: 2026-05-24T19:30:00Z
generator: claude-opus-4-7
business_value_confidence: 0.92
effort_tshirt: M
---

# ChordVoicingsSkill High-Value Test Plan

`ChordVoicingsSkill` is the **top-1 chatbot skill** — the single most-called
deterministic skill in the orchestrator router. It owns NL → typed query →
encoder → OPTIC-K search → markdown rendering for every "voicings for X"
prompt. Annotated `@ai:business-value conf=0.92`.

## Coverage gap summary

`Tests/Common/GA.Business.ML.Tests/Unit/ChordVoicingsSkillTests.cs` already
covers `CanHandle` positive/negative thoroughly (good — that's the
router-collision surface). Gaps in `ExecuteAsync`:

- No test of the **`hasIntent=false` branch** (extractor finds nothing →
  helpful guidance response with `Confidence=0.2f` and the assumption text).
- No test of the **"zero hits with filters, retry without filters" workaround**
  documented in `docs/solutions/architecture/2026-05-08-voicing-search-corpus-tagging-mismatch.md`
  — a regression here would silently break every filter-bearing query.
- No test that **`Confidence` is clamped to [0,1]** from the top-hit score
  (`Math.Clamp(results[0].Score, 0.0, 1.0)`).
- No test that the **markdown rendering** produces `- **{ChordName}** \`{Diagram}\` (...)` lines
  (the React `MarkdownAnswer` component depends on this exact shape).
- No test of the **`Evidence` cap at 5** (`Take(Math.Min(results.Count, 5))`).
- No test of the **`Data.interpreted` payload** — the React `EvidencePanel` reads
  `structured.ChordSymbol`/`ModeName`/`Tags` out of this for the trace UI.

## Test cases (7 proposed)

| # | Name | Type | What it covers | Fixtures / mocks | Overlaps |
|---|---|---|---|---|---|
| 1 | `ExecuteAsync_NoIntent_ReturnsGuidanceWithLowConfidence` | unit | Extractor returns all-null → response.Confidence == 0.2f, Assumptions contains "no musical entities", search service not called. | stub `IMusicalQueryExtractor` returning empty `StructuredQuery`. | none. |
| 2 | `ExecuteAsync_ZeroHitsWithFilters_RetriesWithoutFilters` | unit | First search returns `[]` with non-null filters → second call to `SearchAsync` with `filters: null` is made and its results returned. | stub `EnhancedVoicingSearchService` recording calls; structured query with tags set. | none. |
| 3 | `ExecuteAsync_ZeroHitsWithoutFilters_ReturnsEmptyMarkerResponse` | unit | Both passes return `[]` → response says "OPTIC-K index returned no matches", Confidence == 0.3f, Data.interpreted is set. | stub returning empty both times. | none. |
| 4 | `ExecuteAsync_ConfidenceClampedToUnitInterval` | unit | Top hit score 1.7 → response.Confidence == 1.0; top hit score −0.3 → response.Confidence == 0.0. | parametric stub. | none. |
| 5 | `ExecuteAsync_MarkdownLineFormat_MatchesReactRendererContract` | unit | Output `Result` contains exactly one `- **{ChordName}** \`{Diagram}\` ({VoicingType}, score X.XXX)` line per hit; case-sensitive. | stub returning 3 hits with known fields. | partial: nothing checks the contract. |
| 6 | `ExecuteAsync_EvidenceCappedAt5` | unit | When service returns 12 hits, `response.Evidence.Count == 5`. | stub returning 12 hits. | none. |
| 7 | `ExecuteAsync_DataPayload_RoundTripsStructuredQuery` | unit | `response.Data.interpreted` exposes ChordSymbol / ModeName / Tags from the extractor (React `EvidencePanel` contract). | stub extractor returning each field in turn. | none. |

## Suggested file locations

- Extend `Tests/Common/GA.Business.ML.Tests/Unit/ChordVoicingsSkillTests.cs`
  with all 7 cases (one file, one fixture, fast).

## Effort estimate

**M** (medium). `MakeSkill()` in the current test passes `null!` for three deps;
this plan needs real stubs for `EnhancedVoicingSearchService`,
`IMusicalQueryExtractor`, and `MusicalQueryEncoder`. The `Search` mock is
non-trivial (it composes the typed query into a vector). Estimate 1–2 dev-days
once a small `FakeVoicingSearchService` helper exists.

## Rubric

The workaround branch (case #2) is the single most important test here — it
guards a documented production bug. Without it, a corpus rebuild that *fixes*
the tagging would silently make the retry a dead branch, and the next regression
that *re-introduces* the bug would go undetected.
