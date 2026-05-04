# C# Skill Retirement Criteria

**Status**: Decision document (not yet operationalized — needs Ollama).
**Predecessor**: `2026-05-03-chatbot-agent-framework-migration-recommendation.md` ("kept as deterministic fast path until parity proven") and `2026-05-03-chatbot-skill-md-migration-completion.md`.
**Quality fix #5 of 5** from the user's 2026-05-03 candid assessment.

## Problem

Each migrated skill currently has TWO implementations:

- The **C# fast path** — `IOrchestratorSkill` class executed directly by the orchestrator when its `IIntent` adapter wins routing. Pure function, no LLM round-trip, ~ms latency.
- The **SKILL.md path** — markdown file under `skills/`, body becomes a system prompt or tool-instruction set; the LLM produces the answer (possibly via MCP tool calls).

The migration recommendation said the C# paths stay "until parity is proven." That phrase was never operationalized. This document defines what "proven" means concretely and how a skill exits the dual-path state.

## What "parity proven" means

A skill `S` has **proven parity** when ALL of the following hold over a measurement window:

1. **Structural parity** — `SkillParityMatrixTests` is green for `S` (PR #97 covers this; passes today for all 10 migrated skills).
2. **Behavioural parity** — for every prompt in `S`'s golden prompt set, the SKILL.md path's output materially matches the C# path's output. "Materially matches" is defined per skill class (see below).
3. **Routing parity** — for every prompt in `S`'s golden set, the production `SemanticIntentRouter` routes BOTH paths to `S` (no LLM fallback).
4. **Soak window** — items 1–3 hold for **3 consecutive nightly runs** with no regressions.
5. **Performance parity** — SKILL.md path's p95 latency for golden prompts is ≤ 5× C# path's p95. (Acknowledges LLM round-trip cost; ranks behaviour above raw speed.)

Once these hold, the skill is **eligible for retirement**. Retirement is opt-in by skill — no big-bang switchover.

## "Materially matches" — per skill class

| Skill class | Match criterion | Rationale |
|---|---|---|
| **Catalog** (beginner-chords, progression-mood, modes) | SKILL.md output contains every signature phrase from the C# `AgentResponse.Result`. Order doesn't matter; LLM phrasing flex is OK. | Catalog data is deterministic; the LLM should just reproduce it. |
| **MCP-tool driven, deterministic** (interval, scale-info, chord-info, fret-span) | Structured tool result fields (e.g. `Notes`, `Quality`, `Span`) match C# output verbatim. LLM-generated prose around the data can vary. | Tool path is deterministic by construction; only prose differs. |
| **MCP-tool driven, hybrid** (chord-substitution, key-identification, progression-completion) | Top-1 candidate matches between paths; top-3 candidate sets overlap by ≥2/3. LLM phrasing flex is OK. | These do search/ranking; small score differences are tolerable. |

## Golden prompt set per skill

Lives at `state/quality/chatbot-qa/parity/<skill-name>.json`. Format:

```json
{
  "skill": "scale-info",
  "version": "1",
  "lastUpdated": "2026-05-04",
  "prompts": [
    {
      "text": "What is C major?",
      "expectedFacts": ["C", "D", "E", "F", "G", "A", "B"],
      "criterion": "all-facts-present"
    },
    {
      "text": "What notes are in F# minor?",
      "expectedFacts": ["F#", "G#", "A", "B", "C#", "D", "E"],
      "criterion": "all-facts-present"
    }
  ]
}
```

Per-skill prompt sets MUST contain:

- ≥3 prompts that exercise the most common user phrasing
- ≥1 prompt that uses an accidental (covers F#, Bb, etc.)
- ≥1 prompt with a known edge case (the bare "What is C major?" pattern that drove PR #94, the enharmonic cases that drove the Spell() drift, etc.)

## Runner cadence

- **Nightly** in CI when Ollama is available (currently blocked — Ollama is wedged on this dev host).
- **On-demand** via a `Scripts/run-parity-suite.ps1` script that operators trigger before considering retirement.

The runner produces `state/quality/chatbot-qa/parity/runs/<date>-<skill>.json` with per-prompt outcomes (pass/fail + latency). The `ix-quality-trend` aggregator (per CLAUDE.md "Instrument before you ship") rolls these up into `docs/quality/README.md`.

## Retirement procedure

When a skill `S` meets all five parity conditions:

1. **Open a focused retirement PR** for `S` (one PR per skill — never bulk).
2. **Remove** the `services.AddOrchestratorSkillIntent<<class>>()` line from `GaPlugin.cs`.
3. **Keep** the C# class for at least one minor version cycle as orphaned code. Annotate with `[Obsolete("Retired 2026-MM-DD; SKILL.md path is canonical. Will be deleted in N.M.")]`.
4. **Rerun** the parity suite for the retired skill — must still pass via the SKILL.md path alone.
5. **Update** the migration completion report to mark the skill as retired.
6. **Soak for one full nightly cycle** with the C# path unwired but still present. If anything breaks, restore the registration line (one-line revert).
7. **Final delete** the C# class in a follow-up PR after the soak.

## Rollback procedure

If a retired skill produces user-visible regressions:

1. Restore the `AddOrchestratorSkillIntent<<class>>()` line in `GaPlugin.cs`. C# fast path returns immediately.
2. Open a regression issue documenting which prompt/criterion failed.
3. Move the skill back to "dual-path" state in this doc.
4. Investigate whether the SKILL.md side needs prompt-engineering tweaks, more example prompts, or a tool fix.

## Suggested first retirement candidate

Once Ollama is back: **interval**. Reasons:

- Smallest tool surface (one method, one input pair, one output).
- Output is unambiguous (interval name + quality + size + semitones), so "materially matches" is easy to express.
- The MCP tool (`ga_interval_compute`) was the first canary (PR #76) and has the most production exposure.
- If retirement works for `interval`, the pattern scales to scale-info and chord-info next.

## What NOT to retire

- **qa-architect** — predates the chatbot migration (PR #66), has no C# fast path equivalent.
- Any skill where the SKILL.md path makes a NEW LLM call that the C# path didn't make. Those are net cost regressions, not migrations.

## Open questions

1. **Who runs the nightly?** CI on PR merge, scheduled GitHub Action, Demerzel pipeline, or local `pwsh` script? Suggested: GitHub Action that runs against a dedicated Ollama instance to avoid dev-host contention.
2. **Prompt-set authoring**: who curates the golden prompts per skill? Suggested: skill author proposes during the retirement PR; user approves.
3. **Score-threshold sensitivity**: does the SKILL.md path use the SemanticIntentRouter's 0.65 threshold, or does retirement require the threshold to clear with margin? Suggested: 0.65 as-is; lower the threshold separately if needed.
4. **What about SKILL.md drift back to LLM?** If a SKILL.md prompt-engineering tweak helps prompt A but breaks prompt B, the parity suite must be the gate. Don't ship SKILL.md tweaks without rerunning the suite for that skill.

## Cross-references

- Structural parity: PR #97 (`Tests/.../SkillParityMatrixTests.cs`)
- Migration completion: `docs/plans/2026-05-03-chatbot-skill-md-migration-completion.md`
- Migration recommendation: `docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md`
- Quality assessment that drove this doc: 2026-05-03 candid assessment from user (see commit history for PRs #94–#99)
