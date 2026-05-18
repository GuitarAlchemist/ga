# Plan: Refactor Chatbot Skills from Static Catalogs to Domain-Backed Adapters

**Date:** 2026-05-13
**Status:** Draft — awaiting sign-off before implementation
**Reversibility:** Two-way door (per skill — each migration is independently reversible)
**Revisit trigger:** First production catalog/domain drift incident, OR completion of pilot (modes skill)
**Author:** Claude Opus 4.7 (1M context) with user-driven architectural critique on 2026-05-12

## Summary

Chatbot skills today are SKILL.md catalogs — frozen markdown bodies served verbatim with no LLM call and no domain delegation. The user identified this as the wrong shape after PR #210 shipped: when asked *"what are other famous modes?"*, the `modes` skill returned only the 7 diatonic modes and could not extend to melodic-minor, harmonic-minor, harmonic-major, symmetric, or folk modes — even though `Common/GA.Domain.Core/Theory/Tonal/ModeCatalog.cs` already contains a typed catalog indexed by `IntervalClassVector` and `PitchClassSetId`. The skill duplicates a subset of domain knowledge as text and rots independently of the real model.

This plan replaces the catalog pattern with **Domain-Backed Skills** — thin adapters that parse the query into a typed domain request, call into existing `GA.Domain.Core` / `GA.Business.Core` code, and format the structured result.

## Problem (one-way door risks)

| | Catalog skill (today) | Domain-backed skill (target) |
|---|---|---|
| Source of truth | Markdown text | `GA.Domain.Core` types |
| Drift safety | None — text and code diverge silently | Compiler errors when domain shape changes |
| Composition | Impossible (text can't be filtered) | Trivial (LINQ over typed records) |
| Coverage | Whatever the author wrote | Whatever the domain models |
| OPTIC-K integration | None | Direct (index lookup is a domain call) |
| F# DSL integration | None | Direct (skill calls into `ga_dsl_eval`) |
| LLM cost | Zero | Optional — for formatting, not reasoning |
| Latency | <5ms | <50ms (domain call + optional format) |

The current architecture imposes a one-way door each time a skill ships: every catalog SKILL.md is a permanent point of drift unless we replace it. The longer we wait, the more catalog skills accumulate and the more knowledge gets stranded.

## Principle: Domain-Backed Skills

A skill is a **3-layer adapter**:

1. **Parse** — take the user's natural-language query, classify into a typed domain request shape. May use deterministic regex, semantic intent, or both.
2. **Delegate** — call into existing `GA.Domain.Core` / `GA.Business.Core` / `GA.Business.ML` code with the typed request. Return structured data.
3. **Format** — serialize the structured result for natural-language output. May use templates (deterministic) or an LLM (just for prose, not for reasoning).

A skill **owns no music-theory knowledge**. Music-theory knowledge lives in the domain. Skills route, format, and integrate.

## Inventory: catalogs to refactor

Audit of `skills/*/SKILL.md` (15 canonical skills as of 2026-05-13). Each row says where domain code already exists and what the migration looks like.

### Tier 1 — duplicates existing domain code (priority refactors)

| Skill | Domain code already present | Migration |
|---|---|---|
| `modes` | `Common/GA.Domain.Core/Theory/Tonal/ModeCatalog.cs`, `ModeFamilyMetadata.cs`, `ModeInfo.cs`, `ModeFormula.cs` indexed by `IntervalClassVector`+`PitchClassSetId` | Replace catalog body with `ModesSkill` that calls `ModeCatalog.TryGetFamily` / iterates `Metadata`. Adds coverage for melodic-minor / harmonic-minor / harmonic-major / symmetric / folk **automatically** because the domain already has them. |
| `circle-of-fifths` | `GA.Domain.Core/Theory/Tonal` — key-signature arithmetic via pitch-class operations | Replace with `CircleOfFifthsSkill` that computes the table from `KeySignature.For(root)` calls. Single source of truth = the domain. |
| `scale-info` | Already partially domain-backed — finish: route ALL "notes of X" queries through `GA.Domain.Core` instead of falling back to LLM |
| `diatonic-chords` | `GA.Business.Core.Harmony` chord-building code (per CLAUDE.md layer 3) | New skill — currently doesn't exist; build domain-backed from day one |
| `relative-key` | Same `Theory.Tonal` pitch-class arithmetic as circle-of-fifths | Compute, don't lookup |
| `transpose` | F# DSL `ga_transpose_chord` closure (per memory: 74 `ga_*` MCP tools) | Already works — formalize as the template |
| `common-tones` | `GA.Domain.Core` set-theory (PitchClassSet intersection) | Already works — formalize as the template |

### Tier 2 — pure pedagogy (catalog is correct shape)

| Skill | Reasoning |
|---|---|
| `practice-routine` | Subjective. No domain ground truth. Catalog is legitimate. |
| `genre-essentials` | Subjective. Same reasoning. |
| `progression-mood` | Subjective curation. Catalog is legitimate. |

### Tier 3 — auto-generated, not catalog (should be dynamic)

| Skill | Reasoning |
|---|---|
| `what-can-you-do` | The chatbot's capability list. **Must be auto-generated** from registered `IIntent`s at startup, not hand-curated. Current static list will rot. |

### Tier 4 — needs domain to ship first

| Skill | Blocked on |
|---|---|
| `voicing-search` | OPTIC-K index not loaded in deployment. Domain (`GA.Business.ML.Search.EnhancedVoicingSearchService`) exists; index binary missing. Skill works locally with index loaded. **Production deployment task, not a skill refactor.** |
| `chord-substitution` | Already domain-backed via ICV-distance computation. Confirmed working in tonight's smoke test ("Suggest substitutions for G7 in a ii-V-I" → 200 with ICV-ranked answer). Template. |

## Pilot: refactor `modes` first

`modes` is the right pilot because:
- It's the skill that surfaced the architectural critique tonight.
- The domain already has `ModeCatalog` with all the families the catalog skill is missing.
- The current catalog body is small and the regression surface is well-bounded.

**Pilot deliverables** (one PR):

1. `Common/GA.Business.ML/Agents/Skills/ModesSkill.cs` — refactor `ExecuteAsync` to call `ModeCatalog.Metadata` and `ModeFamilyMetadata.ModeNames` for the list, `ModeFormula` for degrees. Strip the SKILL.md catalog body to frontmatter-only.
2. `skills/modes/SKILL.md` — keep frontmatter (name, description, triggers, scope) — **delete the markdown body**, replace with a one-line pointer to `ModesSkill.cs`.
3. Smoke test: `ModesSkillDomainBackedTests` asserts that:
   - "what are the diatonic modes" returns Ionian–Locrian, sourced from `ModeCatalog`
   - "what are the modes of melodic minor" returns Lydian-dominant, super-Locrian, etc.
   - "what mode has a raised 4th and flat 7?" filters by formula (Lydian dominant) — the compositional query the catalog could never answer
4. Trace step: `skill.modes.domain_lookup` showing which `IntervalClassVector` was queried.

**Pilot success metric:**
- Every query in the smoke test returns content sourced from `ModeCatalog`, asserted by either (a) a trace step naming the domain call, or (b) a contract test that mutates `ModeCatalog` and observes the skill's output change.

## Migration sequence

After pilot:

1. **Tier 1 skills, one PR each, in priority order:** `circle-of-fifths`, `relative-key`, `diatonic-chords`, `scale-info`. Each PR follows the pilot template.
2. **Tier 3:** `what-can-you-do` becomes a `WhatCanYouDoSkill` that enumerates `IServiceProvider.GetServices<IIntent>()` at request time. Single PR.
3. **Tier 4:** Track separately. `voicing-search` becomes functional when the OPTIC-K index ships.
4. **Tier 2:** No change. Catalog is correct shape for subjective pedagogy.

Each PR includes:
- Domain code refactor (skill calls into existing domain types)
- SKILL.md frontmatter-only (body deleted, replaced with cross-reference)
- Smoke test asserting domain-sourcing
- Trace step naming the domain call

## Compound safeguards

| Mechanism | Existing? | Purpose |
|---|---|---|
| `ChatbotShowcaseSmokeTests.EveryShowcasePrompt_ProducesAUsefulAnswer` (PR #210) | Yes | Catches the "advertised path is broken" class |
| `DomainBackedSkillContractTests` | **NEW** — to be written with pilot | Each Tier-1 skill has a test that mutates the domain (e.g. add a fake mode to `ModeCatalog`) and asserts the skill's output reflects the mutation. Locks the delegation contract. |
| Solution doc: `docs/solutions/best-practices/showcase-demo-end-to-end-qa-2026-05-12.md` (PR #210) | Yes | The "click through before declaring done" rule |
| Memory: `feedback_ui_click_through_before_done.md` | Yes | Personal compound for the same |
| **NEW** solution doc: `docs/solutions/best-practices/domain-backed-skills-2026-05-13.md` | To write alongside pilot | The architectural principle — what makes a "good" skill, what makes a catalog smell |

## Anti-goals (what this plan does NOT propose)

- **Not a chatbot rewrite.** Routing, trace, hub, SSE are all fine — the skill ADAPTER is what changes.
- **Not an LLM-first move.** Domain calls are cheap and deterministic; LLM is optional formatter only.
- **Not a SKILL.md elimination.** Frontmatter (triggers, scope, examples) stays — the *body* shrinks to a cross-reference.
- **Not a Tier 2 refactor.** Pedagogy / opinion content stays in catalogs.

## Open questions for the user

1. **Pilot timing:** ship the `modes` pilot this week, or batch with the trace expansion?
2. **LLM as formatter:** OK to use Claude/Ollama for prose formatting in the pilot, or strict templates only?
3. **Tier 4 voicing-search:** is loading OPTIC-K in deployment a separate plan, or wrap it into this one?

## Sign-off

User approval required before pilot ships. This plan does not commit to a delivery date — it commits to an architecture direction. Each subsequent PR is independently scoped.
