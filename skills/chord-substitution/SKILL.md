---
Name: "chord-substitution"
Description: "Finds harmonic substitutions for a chord, or classifies the relationship between two chords (tritone sub, secondary dominant, backdoor dominant, set-class equivalent, ICV neighbor). Calls deterministic Grothendieck-ICV math via MCP tools — never recall theory rules from training data."
Triggers:
  - "substitute"
  - "substitution"
  - "reharmonize"
  - "reharmoni"
  - "instead of"
  - "alternative chord"
  - "swap chord"
  - "replace chord"
  - "tritone sub"
  - "tritone equivalent"
  - "secondary dominant"
  - "backdoor"
  - "related chord"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "ported from Common/GA.Business.ML/Agents/Skills/ChordSubstitutionSkill.cs — fifth MCP-tool-driven canary; first to expose multiple tool methods on one class"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_chord_substitutions
  - ga_chord_compare
---

# Chord Substitution Analysis

When a user asks for harmonic substitutions or wants to compare two chords, call the appropriate MCP tool. Do NOT recall the theory rules from training knowledge — the relationships hinge on pitch-class arithmetic and Grothendieck ICV distance, both of which an LLM is unreliable at.

## Two operations

### 1. `ga_chord_substitutions(chordSymbol)` — single source

Use when the user names ONE chord and asks for alternatives:

- *"What are some substitutions for Cmaj7?"*
- *"Reharmonize G7 for me."*
- *"Alternatives to F#m?"*

Returns up to 5 nearby chords ranked by Grothendieck ICV cost (lower = closer). Each entry has `Name`, `Cost`, and `L1Delta`.

### 2. `ga_chord_compare(chordA, chordB)` — relationship between two

Use when the user names TWO chords and asks how they relate:

- *"Is G7 a tritone sub for Db7?"*
- *"How are Am and C related?"*
- *"Can I use Eb7 instead of A7?"*

Returns a list of named relationships and the ICV L1 distance. Possible relationship types:

| Type | When it fires |
|---|---|
| `Tritone Substitution` | Both chords are dominant 7ths and roots are 6 semitones apart |
| `Secondary Dominant` | A is a perfect 5th above B (A functions as V of B) |
| `Backdoor Dominant` | A is bVII7 of B (resolves up a whole step) |
| `Set-Class Equivalent` | Same prime form under T/I equivalence |
| `ICV Neighbor (L1 = N)` | Grothendieck L1 distance ≤ 2 |
| `Harmonic Distance` | Catch-all when no specific relationship triggered |

Multiple relationships can co-fire on the same pair (a tritone sub is also typically an ICV neighbor).

## Picking which tool to call

Count the chord symbols in the user message:

- 1 chord → call `ga_chord_substitutions(chordSymbol)`.
- 2 chords → call `ga_chord_compare(chordA, chordB)`. **Argument order matters** for secondary-dominant and backdoor-dominant classifications. Pass the first-named chord as `chordA` and the second as `chordB` — the tool's classifications are directional.

## Supported chord symbols

Both tools accept the same syntax: root letter (A-G) + optional accidental (`#` / `b`) + optional quality suffix.

| Suffix | Quality |
|---|---|
| (none) | major |
| `m` / `min` | minor |
| `dim` | diminished |
| `aug` / `+` | augmented |
| `7` | dominant 7 |
| `maj7` | major 7 |
| `m7` / `min7` | minor 7 |
| `m7b5` | half-diminished 7 |
| `dim7` | diminished 7 |

Extended / altered chords (sus, 9, 11, 13, slash chords) are out of scope — the tools will return an Error.

## Phrasing the answer

For substitutions, list the candidates with cost, e.g.:

> Harmonic substitutions for **Cmaj7** (ranked by ICV distance):
> - **Am7** — cost 0.50 (L1 = 1)
> - **Em7** — cost 0.83 (L1 = 2)

For comparisons, lead with the strongest relationship, then mention secondary ones:

> **G7 → Db7** are a **Tritone Substitution**: roots 6 semitones apart, both dominant 7ths. M3 of G7 = m7 of Db7 — guide tones shared. (Also flagged as ICV Neighbor with L1 = 0.)

If `Error` is non-null, surface the message verbatim and ask the user to clarify.

## Out of scope

- **Extended chords** (sus, 9, 11, 13, slash chords) — not supported.
- **Voice-leading optimization** — the tool ranks by ICV cost, not by smoothness of common-tone retention. For voice-leading queries, defer to a different skill.
- **Functional harmony beyond V/I and bVII/I** — Phrygian dominants, Neapolitan chords, etc. are not specifically classified; they'll fall through to the generic `Harmonic Distance` result.

## Cross-reference

- MCP tool: `Common/GA.Business.ML/Agents/Mcp/ChordSubstitutionMcpTools.cs`
- Tool tests: `Tests/Common/GA.Business.ML.Tests/Unit/ChordSubstitutionMcpToolsTests.cs`
- Legacy C# skill it replaces: `Common/GA.Business.ML/Agents/Skills/ChordSubstitutionSkill.cs`
