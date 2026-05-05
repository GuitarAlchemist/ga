---
name: "interval"
description: "Computes the simple interval between two named pitches (e.g. C to G is a perfect fifth). Calls the deterministic `ga_interval_compute` MCP tool — never recall an answer from training data."
triggers:
  - "interval between"
  - "interval from"
  - "what is the interval"
  - "distance from"
  - "distance between"
  - "how many semitones"
  - "semitones from"
  - "semitones between"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "first MCP-tool-driven canary; replaces direct C# IntervalSkill in the SKILL.md path"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_interval_compute
---

# Interval Between Two Notes

When a user asks for the interval, distance, or semitone count between two named pitches, you MUST call the `ga_interval_compute` MCP tool. Do NOT compute the answer from training knowledge — the tool is deterministic and the model is not.

## Calling the tool

The tool takes two arguments:

- `lowerNote` — string, e.g. `"C"`, `"F#"`, `"Bb"`. Case-insensitive.
- `upperNote` — string, same notation.

The tool returns a structured result with these fields:

- `Name` — short interval name (e.g. `"P5"`, `"m3"`).
- `Quality` — long form: `"perfect"`, `"major"`, `"minor"`, `"augmented"`, `"diminished"`.
- `Size` — long form: `"unison"`, `"second"`, `"third"`, ..., `"octave"`.
- `Semitones` — integer 0–12.
- `Error` — non-null only when input could not be parsed.

## Picking the lower / upper note

Map the user's phrasing to argument order:

- *"interval from C to G"* → `lowerNote="C"`, `upperNote="G"`.
- *"interval between C and G"* → same as above; the first-mentioned note is the lower.
- *"distance from F# to D"* → `lowerNote="F#"`, `upperNote="D"`. Pass both in order even when the lower is alphabetically later — the tool computes the simple interval correctly.
- *"how many semitones from A to E"* → `lowerNote="A"`, `upperNote="E"`. Read out the `Semitones` field to answer.

## Phrasing the answer

Use the tool's `Quality` and `Size` words verbatim, then quote the short `Name` and `Semitones` count. Example:

> From **C** to **G** is a **perfect fifth** (P5, 7 semitones).

If `Error` is non-null, surface the message verbatim and ask the user to clarify the note names.

## When to ask for clarification

If the user provides only one note, or names a chord rather than two notes (e.g. *"interval of A"*, *"what's the interval in a Cmaj7"*), do NOT call the tool with placeholder values — ask for the missing pitch first. Example response: *"Could you give me both notes? E.g. 'interval from C to G' or 'distance between F# and A'."*

## Out of scope

- **Compound intervals** (9th, 13th, etc.) — the tool returns the simple interval. If the user asks *"interval from C to high D"*, call the tool with `C` and `D` (which gives a major second), then in the answer flag the octave assumption explicitly: *"I'm treating 'high D' as D one octave above C, so the compound interval is a major ninth (M2 plus an octave). If you meant a different D, let me know."* Always state which D you assumed.
- **Microtonal / just-intonation** intervals — the tool only handles the standard 12-tone system. Decline cleanly: *"This skill computes intervals in 12-TET only; for cents-based answers I'd need different tooling."*

## Cross-reference

- MCP tool: `Common/GA.Business.ML/Agents/Mcp/IntervalMcpTools.cs`
- Tool tests: `Tests/Common/GA.Business.ML.Tests/Unit/IntervalMcpToolsTests.cs`
- Legacy C# skill it replaces: `Common/GA.Business.ML/Agents/Skills/IntervalSkill.cs` (regex-driven, no LLM round-trip — kept for the deterministic fast path)
