---
name: "transpose"
description: "Transposes a chord by a named musical interval (e.g. Cmaj7 up a perfect fourth = Fmaj7). Calls the deterministic `ga_transpose_chord` MCP tool — never recall transpositions from training data, since LLMs commonly produce wrong enharmonics for less-common keys (Db major vs C# major)."
triggers:
  - "transpose"
  - "move this chord"
  - "shift this chord"
  - "up a"
  - "down a"
  - "in the key of"
  - "change the key"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 1 daily-use skill (skill-stewards 2026-05-05)"
  blocked_on: "ga_transpose_chord MCP tool — not yet implemented in Common/GA.Business.ML/Agents/Mcp/"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_transpose_chord
---

# Transpose a Chord

When a user asks to transpose a chord by an interval (or to move it into a different key), call `ga_transpose_chord`. Never compute the transposition mentally — for less-common keys (Gb major, C# minor) the LLM will confidently flip enharmonics and produce wrong spellings.

## Calling the tool

Arguments:

- `chordSymbol` — string, e.g. `"Cmaj7"`, `"F#m"`, `"Bb7"`.
- `interval` — interval name OR semitones. Accepts `"perfect fourth"`, `"minor third"`, `"P4"`, `"m3"`, OR `5` (semitones).
- `direction` — `"up"` (default) or `"down"`.

Returns:

- `Original` — the input chord.
- `Transposed` — the resulting chord symbol.
- `Notes` — the new chord's tones in canonical spelling.
- `Error` — non-null when the input couldn't be parsed.

## Mapping user phrasings

- *"Transpose Cmaj7 up a perfect fourth"* → `chordSymbol="Cmaj7", interval="perfect fourth", direction="up"`.
- *"Move this F chord down a minor third"* → `chordSymbol="F", interval="minor third", direction="down"`.
- *"What's Dm7 up a whole step?"* → `chordSymbol="Dm7", interval="major second", direction="up"`.
- *"Cmaj7 in the key of G"* — needs interpretation: G is up a perfect fifth from C, so `interval="perfect fifth", direction="up"`. If ambiguous (could go down), surface the assumption.

## Phrasing the answer

Lead with the result; mention the new notes for educational color:

> **Cmaj7 up a perfect fourth = Fmaj7** (F – A – C – E).

## When to refuse / clarify

- *"Transpose this whole progression"* — call this skill once per chord, OR defer to the progression-analysis skill which can handle whole sequences.
- Keys named ambiguously (e.g. *"in C"* — major or minor?) — pick major as default but state the assumption.

## Out of scope

- Bulk progression transposition — separate prompt per chord.
- Slash chords (C/E up a fifth = G/B) — the tool may not preserve the bass note correctly; flag in the response.

## Cross-reference

- MCP tool: `ga_transpose_chord` (Common/GA.Business.ML/Agents/Mcp/ChordMcpTools.cs)
