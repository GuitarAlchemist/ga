---
name: "common-tones"
description: "Finds notes shared between TWO chords and describes their interval role in each (root / 3rd / 5th / 7th / extension). Used for pivot-chord choice, smooth voice leading, and modulation prep. Calls the deterministic `domain.commonTones` closure via the `ga_dsl_eval` MCP tool — never compute pivot tones from training data, since LLMs fumble extension intervals on altered chords."
triggers:
  - "common tones"
  - "common tone"
  - "shared notes"
  - "pivot tone"
  - "pivot chord"
  - "notes in common"
  - "what do these chords share"
  - "what notes do x and y share"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "graduated 2026-05-06 — second canary for ga_dsl_eval (Phase 2b)"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_dsl_eval
last_verified: 2026-05-06
---

# Common Tones Between Two Chords

When a user asks what notes two chords share, call **`ga_dsl_eval`** with closure name **`domain.commonTones`**. Don't compute the intersection mentally — the closure returns each shared note's interval role in BOTH chords (root / 3rd / 5th / 7th / 9th / 11th / 13th), and that role-mapping is exactly what the LLM gets wrong on extended/altered chords.

## Why this skill uses `ga_dsl_eval` (not a keyhole tool)

This is the second canary for the DSL-eval pattern (PR #146 was the first, transpose). The `domain.commonTones` closure is already in `GaClosureRegistry` — exposed because its category is `Domain`. See `docs/contracts/2026-05-06-ga-dsl-eval-contract.md` (v0.1).

## Calling the tool

Method: `ga_dsl_eval`

Args (flat key-value, all values as strings):

- `closureName` = `"domain.commonTones"`
- `args.chord1` — first chord symbol, e.g. `"Cmaj7"`, `"F#m7"`.
- `args.chord2` — second chord symbol.

Example invocation:

```
ga_dsl_eval(closureName: "domain.commonTones",
            args: { "chord1": "Cmaj7", "chord2": "Am7" })
```

Returns a `DslEvalResult`. Read `Result` for the formatted shared-notes string (or `ResultJson` for the same value as a JSON-quoted string). On failure, read `Error.Code` and `Error.Message`.

## Mapping user phrasings

- *"What notes do Cmaj7 and Am7 share?"* → `args = { "chord1": "Cmaj7", "chord2": "Am7" }`
- *"Common tones between G and D7"* → `args = { "chord1": "G", "chord2": "D7" }`
- *"Pivot tones from F to Bb7"* → `args = { "chord1": "F", "chord2": "Bb7" }`
- *"What's shared between Cmaj9 and Em7?"* → `args = { "chord1": "Cmaj9", "chord2": "Em7" }`

## Phrasing the answer

The closure returns a formatted string already. Surface it verbatim, then add a one-line interpretive hook tailored to the user's apparent intent (pivot, voice leading, modulation prep). Example:

> **Cmaj7 and Am7** share these notes:
> - C → root of Cmaj7, minor third of Am7
> - E → major third of Cmaj7, perfect fifth of Am7
> - G → perfect fifth of Cmaj7, minor seventh of Am7
>
> Three common tones — that's why Am7 is the canonical relative-vi substitute for C. Swap them anywhere in a progression with minimal harmonic disturbance.

If the closure returns *"X and Y share no common tones"*, surface that and explain what it means: harmonically distant chords (often a tritone-related pair, an altered-dominant against a parallel major, or genuinely opposing modal centres). Suggest checking interval distance via the `interval` skill or using `chord-substitution` to find a related pair.

## When to refuse / clarify

- **Single chord** — that's a `chord-info` question (notes of one chord). Defer.
- **Three or more chords** — out of scope for v0.1 (closure takes pairs). Either compute pairwise (Cmaj7-Am7, Am7-F, Cmaj7-F) or ask the user which pair matters most.
- **Unparseable chord symbol** — the closure returns a `ParseError` naming which chord (`chord1` or `chord2`) failed; surface the error and ask for the correct symbol.

## Out of scope

- **N-way common tones (N ≥ 3)** — closure takes pairs only. Wrap with multiple calls if you need a 3-way intersection (compute pairwise, then intersect the result sets in prose).
- **Voice-leading optimisation** — separate skill (BACKLOG #139 gap, not yet shipped). Common tones are a sub-step of voice leading; this skill returns the intersection, not the optimal motion of all voices.
- **Modulation suggestions** — knowing the common tones is foundational, but choosing a destination key for modulation is a richer query. Defer to `key-identification` for analysing existing progressions.

## Cross-reference

- MCP tool surface: `ga_dsl_eval` in `Common/GA.Business.ML/Agents/Mcp/DslEvalMcpTools.cs`
- Closure: `domain.commonTones` in `Common/GA.Business.DSL/Closures/BuiltinClosures/DomainClosures.fs` (line ~489)
- Contract: `docs/contracts/2026-05-06-ga-dsl-eval-contract.md` (v0.1)
- Plan: `docs/plans/2026-05-06-skills-orchestration-architecture.md` (Phase 2b — second canary)
- Sibling: `transpose` (first DSL-eval canary, PR #146)
