---
Name: "scale-info"
Description: "Returns the seven notes of a major or minor key plus its key signature and relative key. Calls the deterministic `ga_scale_get_notes` MCP tool — never recall an answer from training data."
Triggers:
  - "notes in"
  - "notes are in"
  - "notes of"
  - "scale of"
  - "scale notes"
  - "what is c major"
  - "what is c minor"
  - "show me the"
  - "list the notes"
  - "key of"
  - "what key"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "second MCP-tool-driven canary; replaces direct C# ScaleInfoSkill in the SKILL.md path"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_scale_get_notes
---

# Notes of a Major or Minor Key

When a user asks for the notes of a key (or scale), call the `ga_scale_get_notes` MCP tool. Do NOT recall the notes from training knowledge — different sources spell the same key differently (D# minor vs Eb minor) and the LLM will confidently produce wrong notes for less-common keys.

## Calling the tool

The tool takes two arguments:

- `root` — string, e.g. `"C"`, `"F#"`, `"Bb"`. Case-insensitive.
- `mode` — `"major"` or `"minor"` (also accepts `"maj"` / `"min"`).

It returns a structured result:

- `Notes` — string array of seven note names in ascending order.
- `KeySignature` — human-readable, e.g. `"2 sharps"` or `"no sharps or flats"`.
- `RelativeKey` — name of the relative major or minor.
- `Error` — non-null only when the input could not be parsed.

## Mapping user phrasings to arguments

- *"What notes are in C major?"* → `root="C"`, `mode="major"`.
- *"Show me the F# minor scale"* → `root="F#"`, `mode="minor"`.
- *"List the notes in Bb major"* → `root="Bb"`, `mode="major"`.
- *"What is D minor?"* / *"Tell me about D minor"* → treat as a notes request: `root="D"`, `mode="minor"`.
- *"What's in the key of E?"* — the user did not specify mode. **Ask first** rather than guessing: *"E major or E minor?"*

## Phrasing the answer

Use the `Notes` array verbatim, joined with `–` (en-dash) or commas. Add the `KeySignature` and `RelativeKey` as a separate sentence. Example:

> The C major scale has 7 notes: **C – D – E – F – G – A – B**. Key signature: no sharps or flats. Relative minor: A minor.

If `Error` is non-null, surface the message verbatim and ask the user to clarify.

## When to ask for clarification

- User names a chord rather than a key: *"what notes are in a C major chord"* → that's a chord-intervals question; defer to the chord-info skill (when implemented). Do NOT call `ga_scale_get_notes`.
- User asks about modes (Dorian, Phrygian, etc.) other than major / minor — the tool only supports major and minor. Decline cleanly: *"This tool returns only major and minor scales; for modes I'd need different tooling."*
- User asks for a scale other than the diatonic 7-note one (whole-tone, blues, pentatonic, harmonic minor): same — decline rather than fabricating notes.

## Out of scope

- **Modal scales** (Dorian, Phrygian, Lydian, etc.) — not yet supported by the tool.
- **Synthetic / non-Western scales** — not supported.
- **Chord-tones-of-a-chord queries** — different skill.

## Cross-reference

- MCP tool: `Common/GA.Business.ML/Agents/Mcp/ScaleMcpTools.cs`
- Tool tests: `Tests/Common/GA.Business.ML.Tests/Unit/ScaleMcpToolsTests.cs`
- Legacy C# skill it replaces: `Common/GA.Business.ML/Agents/Skills/ScaleInfoSkill.cs` (regex-driven, no LLM round-trip — kept for the deterministic fast path)
