---
Name: "interval"
Description: "Computes the simple interval between two named pitches (e.g. C to G is a perfect fifth). Calls the deterministic `ComputeInterval` MCP tool — never recall an answer from training data."
Triggers:
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
  - ComputeInterval
---

# Interval Between Two Notes

When a user asks for the interval, distance, or semitone count between two named pitches, you MUST call the `ComputeInterval` MCP tool. Do NOT compute the answer from training knowledge — the tool is deterministic and the model is not.

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

## Out of scope

- Compound intervals (9th, 13th, etc.) — the tool returns the simple interval; if the user asks "interval from C to high D", treat as a 9th by adding an octave to the simple result, but flag the assumption.
- Interval qualities for notes outside the standard 12-tone system (microtones, just-intonation cents) — not supported.

## Cross-reference

- MCP tool: `Common/GA.Business.ML/Agents/Mcp/IntervalMcpTools.cs`
- Tool tests: `Tests/Common/GA.Business.ML.Tests/Unit/IntervalMcpToolsTests.cs`
- Legacy C# skill it replaces: `Common/GA.Business.ML/Agents/Skills/IntervalSkill.cs` (regex-driven, no LLM round-trip — kept for the deterministic fast path)
