---
name: "relative-key"
description: "Returns the relative major / minor / parallel key for a given key. Calls the deterministic `ga_relative_key` MCP tool. Use when a learner asks 'what's the relative minor of X' / 'parallel key' / 'relative major' / 'is X major and Y minor related'."
triggers:
  - "relative minor"
  - "relative major"
  - "relative key"
  - "parallel key"
  - "parallel minor"
  - "parallel major"
  - "related key"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 1 daily-use skill (skill-stewards 2026-05-05)"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_relative_key
---

# Relative / Parallel Keys

Common pedagogy pair-skill with `key-identification`. When a user asks for the relative or parallel of a key, call `ga_relative_key`. Don't rely on training-data shortcuts — the LLM can flip enharmonic spellings (Cb major / B major) and Phrygian-style relative-of-the-mode questions need explicit tool support.

## Calling the tool

Arguments:

- `key` — string, e.g. `"C major"`, `"A minor"`, `"F# major"`.
- `relation` — `"relative"` (major↔minor with same key signature), `"parallel"` (same root, opposite mode), or `"all"` (returns both).

Returns:

- `OriginalKey` — echoed.
- `Relative` — { name, sharedKeySignature, accidentals }.
- `Parallel` — { name, accidentalDelta } when applicable.

## Mapping user phrasings

- *"What's the relative minor of C major?"* → `key="C major", relation="relative"` → A minor.
- *"Relative major of A minor?"* → `key="A minor", relation="relative"` → C major.
- *"Parallel minor of D major?"* → `key="D major", relation="parallel"` → D minor.
- *"Are C major and A minor related?"* — confirm yes via `relative` lookup; explain shared key signature (no sharps or flats).

## Phrasing the answer

Lead with the answer, mention the shared key signature for educational color:

> The **relative minor of C major** is **A minor** — both share the empty key signature (no sharps or flats).

For parallel:

> The **parallel minor of D major** is **D minor** — same root, but D minor has one flat (Bb) instead of D major's two sharps (F#, C#).

## When to refuse / clarify

- *"Relative key of D Dorian"* — modes don't have "relative" in the major/minor sense; explain or defer to a modal-relationships skill.
- Ambiguous keys (just *"in A"*) — assume major and state the assumption.

## Out of scope

- **Modal relatives** (Dorian, Phrygian, etc.) — separate skill.
- **Negative harmony** / **mirror keys** — out of scope.

## Cross-reference

- MCP tool: `ga_relative_key` (Common/GA.Business.ML/Agents/Mcp/ScaleMcpTools.cs)
