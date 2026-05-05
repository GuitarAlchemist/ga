---
name: "easier-voicings"
description: "Returns simpler / beginner-friendly fingerings of a chord, ranked by playability. Calls the deterministic `ga_easier_voicings` MCP tool. Use when a learner says 'this chord is too hard' / 'easier version' / 'beginner voicing'."
triggers:
  - "easier voicing"
  - "easier version"
  - "simpler chord"
  - "beginner version"
  - "easier way to play"
  - "too hard to play"
  - "simpler fingering"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 2 skill (skill-stewards 2026-05-05)"
  blocked_on: "ga_easier_voicings MCP tool — not yet implemented in Common/GA.Business.ML/Agents/Mcp/"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_easier_voicings
  - ga_fret_span
---

# Easier / Simpler Chord Voicings

When a user says a chord is too hard, call `ga_easier_voicings`. The tool ranks fingering options by playability score (fret span, finger count, barre presence). LLM-recalled "easier voicings" are unreliable — what looks easier in tab notation may require a wider span.

## Calling the tool

Arguments:

- `chord` — chord symbol, e.g. `"Bbmaj7"`, `"F#m"`.
- `instrument` — `"guitar"` (default), `"ukulele"`, etc. — defaults to standard 6-string guitar tuning.
- `maxFretSpan` — optional cap (e.g. `3` to require 3-fret reach max).
- `excludeBarre` — `true` to filter out barre voicings.

Returns:

- `Chord` — echoed.
- `Voicings` — ranked array `{ name, fretboardDiagram, fretSpan, fingerCount, requiresBarre, playabilityScore }`.
- `BestPick` — top-ranked option.

## Mapping user phrasings

- *"Easier way to play F"* → `chord="F", excludeBarre=true`. Returns Fadd9 substitute or partial-F shapes.
- *"Beginner voicing for Bbmaj7"* → `chord="Bbmaj7", maxFretSpan=3`.
- *"Simpler version of this jazz chord"* — ask which jazz chord; voicing-search is a better dispatch if the user hasn't named it.

## Phrasing the answer

Lead with the easiest option's diagram + a short rationale:

> Try this 3-finger F voicing (no barre):
>
> ```
> e|--1--
> B|--1--
> G|--2--
> D|--3--
> A|--x--
> E|--x--
> ```
>
> Skip the low E and A strings. Span = 2 frets, 3 fingers. Easier alternative if the full barre is fighting you.

## When to refuse / clarify

- *"How do I get better at barre chords?"* — pedagogy question; defer to a future technique skill.
- Chord too far outside catalog — flag the empty result.

## Out of scope

- **Hand-size customisation** — the tool uses a default ergonomic model; doesn't know the player's reach.
- **Capo workarounds** — separate skill (could be added).

## Cross-reference

- MCP tool: `ga_easier_voicings` (Common/GA.Business.ML/Agents/Mcp/VoicingMcpTools.cs)
- Companion: `fret-span` for playability scoring of a specific user-supplied diagram.
