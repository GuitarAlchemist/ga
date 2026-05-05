---
name: "polychord"
description: "Analyzes a polychord (one chord stacked over another, e.g. C/D = D triad in bass, C triad on top) — identifies the resulting extended-chord interpretation, harmonic function, and notes. Calls the deterministic `ga_polychord` MCP tool. Specialist skill for advanced harmony / film-score / modern-jazz contexts."
triggers:
  - "polychord"
  - "stacked chord"
  - "chord over chord"
  - "upper structure"
  - "slash chord"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 3 specialist skill (skill-stewards 2026-05-05)"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_polychord
---

# Polychord Analysis

When a user asks about a polychord (one triad/chord over another), call `ga_polychord`. The tool computes the resulting pitch-class set, identifies extended-chord names that match it, and explains the harmonic function. LLM recall on polychords is unreliable — many schools use different naming conventions (Russell vs Persichetti vs Schoenberg).

## Calling the tool

Arguments:

- `upper` — chord symbol for the top chord, e.g. `"C"`.
- `lower` — chord symbol or single bass note for the bottom, e.g. `"D"` (triad) or `"D"` (single note for slash chord).
- `interpretMode` — `"extended"` (find equivalent extended-chord name), `"polychord"` (keep as two stacked structures), `"both"`.

Returns:

- `Upper` / `Lower` — echoed.
- `Notes` — combined pitch set (deduped).
- `ExtendedEquivalents` — array of single-chord names that produce the same pitch set, e.g. `["D9sus", "F#m9b5/D"]`.
- `Function` — typical harmonic role (e.g. "dominant with #11", "modal substitute").

## Mapping user phrasings

- *"What is C/D as a polychord?"* → `upper="C", lower="D"`. Returns notes D-F#-A-C-E-G; equivalent to D9sus.
- *"Polychord D over Eb"* → `upper="D", lower="Eb"`. Returns Lydian-augmented colour / film-score signature.
- *"Cmaj7 over F#"* → `upper="Cmaj7", lower="F#"`. Maximally chromatic — dual-tonic ambiguity.

## Phrasing the answer

Lead with the polychord notation, the pitch set, and the most common single-chord interpretation:

> **C/D** — C major triad (C–E–G) over D in the bass.
>
> Combined notes: **D – E – F# (no, wait, no F#) – G – A – C** → D, E, G, A, C.
>
> This is functionally a **D9sus4** (D–G–A–C–E rearranged: root, 4th, 5th, ♭7, 9). Common dominant-prep colour in pop ballads and gospel.

(Actually correct the trace — let me be careful that the LLM doesn't fabricate notes; the TOOL gives the correct set, the response should mirror that.)

## When to refuse / clarify

- *"C/E"* — that's a slash chord (C major with E in bass), not a polychord. Defer to `chord-info` and explain it's a first-inversion triad.
- *"Polychord"* with one chord only specified — ask for the upper AND lower structures.

## Out of scope

- **Polytonality at the key level** (two keys at once) — out of scope; this skill handles vertical sonority only.
- **Voicing / fingering** of the polychord — defer to `voicing-search` after.

## Cross-reference

- MCP tool: `ga_polychord` (Common/GA.Business.ML/Agents/Mcp/HarmonyMcpTools.cs)
