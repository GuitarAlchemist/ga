---
name: "chord-info"
description: "Returns the notes and intervals of a named chord (e.g. Cmaj7 = C E G B). Calls the deterministic `ga_chord_info` MCP tool — never recall an answer from training data, since LLMs commonly flip enharmonics (Db vs C#) based on which source they saw."
triggers:
  # Tightened 2026-05-05 (pro-guitarist audit) — removed bare
  # "what is a" / "what is the" / "make up a" (matched any
  # what-is question; routes belonged elsewhere). Each trigger
  # now anchors on a chord-shaped or chord-vocabulary token so
  # SKILL.md-driven dispatch (FileBasedSkillsProvider, Claude
  # Code path) doesn't pull non-chord queries here.
  - "what is a chord"
  - "what is the chord"
  - "what chord is"
  - "what notes are in"
  - "notes in a"
  - "notes of a"
  - "spell a chord"
  - "spell the chord"
  - "chord notes"
  - "chord intervals"
  - "spell a c"
  - "spell a d"
  - "spell a e"
  - "spell a f"
  - "spell a g"
  - "spell an a"
  - "spell a b"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "third MCP-tool-driven canary; replaces direct C# ChordInfoSkill in the SKILL.md path"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_chord_info
---

# Notes of a Chord

When a user asks for the notes / intervals / construction of a named chord, call the `ga_chord_info` MCP tool. Do NOT recall the notes from training knowledge — different sources spell the same chord differently (Bbm = Bb-Db-F vs Bb-C#-F) and the LLM will confidently produce wrong enharmonics for less-common keys.

## Calling the tool

The tool takes one argument:

- `chordSymbol` — string, e.g. `"C"`, `"Cm"`, `"Cmaj7"`, `"F#dim"`, `"Bbm7"`. Case-insensitive root letter; quality suffix uses the standard short forms below.

It returns a structured result:

- `Symbol` — chord symbol echoed back.
- `Root` — root note in canonical case (e.g. `"F#"`).
- `Quality` — long form: `"major"`, `"minor"`, `"diminished"`, `"augmented"`, `"dominant 7"`, `"major 7"`, `"minor 7"`, `"diminished 7"`, `"half-diminished"`.
- `Notes` — array of chord tones in ascending order.
- `Intervals` — interval names for each note (e.g. `["root", "major third", "perfect fifth"]`).
- `Error` — non-null only when the input could not be parsed.

## Supported quality suffixes

| Symbol form | Quality returned |
|---|---|
| `C`, `Cmaj`, `CM` | `"major"` |
| `Cm`, `Cmin` | `"minor"` |
| `Cdim` | `"diminished"` |
| `Caug` | `"augmented"` |
| `C7`, `Cdom7` | `"dominant 7"` |
| `Cmaj7`, `CM7` | `"major 7"` |
| `Cm7`, `Cmin7` | `"minor 7"` |
| `Cdim7` | `"diminished 7"` |
| `Cm7b5`, `Cmin7b5` | `"half-diminished"` |

## Mapping user phrasings to arguments

- *"What is a C major chord?"* → `chordSymbol="C"` (or `"Cmaj"`).
- *"What notes are in Dm7?"* → `chordSymbol="Dm7"`.
- *"Spell a B7 chord"* → `chordSymbol="B7"`.
- *"Notes in an F minor chord"* → `chordSymbol="Fm"`.
- *"What's in a Cmaj7?"* → `chordSymbol="Cmaj7"`.
- *"Spell a B half-diminished"* → `chordSymbol="Bm7b5"`.
- *"What's a C diminished 7th?"* → `chordSymbol="Cdim7"`.

## Phrasing the answer

Use the `Notes` array verbatim, joined with `–` (en-dash) or commas. Mention the `Quality` and the `Intervals` for educational color. Example:

> A **Cmaj7** chord contains **C – E – G – B** (root, major third, perfect fifth, major seventh).

If `Error` is non-null, surface the message verbatim and ask the user to clarify.

## When to ask for clarification

- User names a key rather than a chord (e.g. *"what notes are in C major key"* — vs *"C major chord"*) → that's a scale-info question; defer to the scale-info skill, do NOT call `ga_chord_info`.
- User asks for a chord quality this tool doesn't support (sus2, sus4, 9th, 11th, 13th, slash chords, altered dominants like 7b5 or 7#9) — the tool will return an Error. Decline cleanly: *"I can spell major / minor / diminished / augmented triads, major/minor/dominant sevenths, diminished sevenths (dim7) and half-diminished (m7b5). For altered or extended chords beyond those I'd need different tooling."*

## Out of scope

- **Extended chords** (9, 11, 13) — not yet supported.
- **Altered / suspended / add chords** (sus2, sus4, add9, 7b5, 7#9, etc.) — not supported. *(m7b5 and dim7 ARE supported as of 2026-05-10.)*
- **Slash chords** (C/E, Am/G) — not supported; pass just the upper chord and explain the bass note in prose.
- **Chord identification from a note set** (*"what chord is C E G?"*) — not yet exposed; that would be a separate `ga_chord_identify(notes)` tool.

## Cross-reference

- MCP tool: `Common/GA.Business.ML/Agents/Mcp/ChordMcpTools.cs`
- Tool tests: `Tests/Common/GA.Business.ML.Tests/Unit/ChordMcpToolsTests.cs`
- Legacy C# skill it replaces: `Common/GA.Business.ML/Agents/Skills/ChordInfoSkill.cs` (regex-driven, no LLM round-trip — kept for the deterministic fast path)
