---
name: "progression-completion"
description: "Suggests 2-3 diatonic cadence completions for an in-progress chord progression. Reuses the `ga_key_identify` MCP tool for deterministic key detection, then names cadence types (authentic / half / deceptive / plagal) drawn from the detected key's diatonic set."
triggers:
  - "what comes next"
  - "next chord"
  - "what should follow"
  - "finish this"
  - "complete this"
  - "end this"
  - "end it"
  - "help me finish"
  - "help me end"
  - "how to end"
  - "how do i end"
  - "continue this"
  - "extend this"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "ported from Common/GA.Business.ML/Agents/Skills/ProgressionCompletionSkill.cs — seventh and final MCP-tool-driven canary; reuses ga_key_identify rather than introducing a new tool"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_key_identify
---

# Progression Completion

When a user asks how to finish, continue, or extend a chord progression, you must NOT invent chord suggestions from training knowledge. The LLM is unreliable at picking diatonic chords for unfamiliar keys. Instead:

1. **First** — call `ga_key_identify` on the user query. It returns the detected key plus the seven diatonic chords (`I`–`vii°`) of that key.
2. **Then** — pick 2-3 cadence candidates from that diatonic set (and ONLY from that set), name the cadence type, and give the Roman numeral.

## Calling the tool

The argument is a string — bare chord list (`"C G Am"`) or full question (`"what comes next after C G Am?"`). The tool's `KeyIdentificationResult` has the same shape as documented in the `key-identification` skill — see that for full field semantics.

The fields you'll use here:

- `TopCandidates[0].Key` — the detected key.
- `TopCandidates[0].DiatonicSet` — the seven chords you may pick from.
- `RecognizedChords` — the chord symbols already in the progression (you typically want the NEXT chord to be different from the last one in this list).

**Tied keys.** When `TopCandidates` has more than one entry (e.g. a progression that fits both C major and A minor), draw your suggestions from `TopCandidates[0].DiatonicSet`. Relative-pair ties share the same diatonic set so the choice is neutral; on the rare non-relative tie (very short progressions), defaulting to the first candidate keeps the answer concrete. Mention the alternative key in passing but only suggest chords from one set.

If `Error` is non-null, surface the message verbatim and ask the user to clarify the chord names.

## Cadence catalog (the four you may name)

Every suggestion you make must be one of these four cadence types:

| Cadence | Roman numerals | Effect |
|---|---|---|
| **Authentic** | V → I (V7→I is stronger) | Strong, conclusive resolution. The "home" feeling. |
| **Half** | anything → V | Open, suspended; the progression "stops on a question". |
| **Deceptive** | V → vi (instead of V → I) | Surprises the ear by avoiding tonic; useful when you want to extend. |
| **Plagal** | IV → I | Soft, "amen" cadence; less assertive than authentic. |

In a minor key, substitute as needed (`V` is conventionally drawn from harmonic minor — the raised 7th degree gives a leading tone — so `V7` is the standard cadential dominant even when only `v` appears in the natural-minor diatonic set).

## How to phrase suggestions

Lead with the key, then offer 2-3 numbered options. For each:

- The chord(s) to play (1-2 chords drawn from `DiatonicSet`).
- The cadence type from the catalog above.
- The Roman numeral(s).
- A one-sentence explanation in plain language.

### Example

User: *"What chord comes next after C G Am?"*

After calling `ga_key_identify("C G Am")` → returns `TopCandidates: [{ Key: "C major", DiatonicSet: [C, Dm, Em, F, G, Am, B°] }, ...]` (also tied with A minor):

> The progression `C G Am` is in **C major** (or its relative A minor — both fit). Here are three ways to continue:
>
> 1. **`F → G → C`** — *authentic cadence* (IV–V–I). Brings everything home cleanly. The classic 1960s-pop ending.
> 2. **`F`** — *plagal cadence* (IV–I). Soft "amen" finish; less dramatic than the authentic resolution.
> 3. **`Dm → G7`** — *half cadence* (ii–V7). Doesn't resolve — leaves the loop open if you're looping the progression.

## Hard constraints (do NOT break these)

- **Every suggested chord must appear in `DiatonicSet`.** No borrowed chords, no chromatic substitutions in this skill's output. If the user wants reharmonization with non-diatonic chords, defer to the `chord-substitution` skill.
- **Pick at most 3 suggestions.** More is overwhelming; one is too few.
- **Do not invent a key the tool didn't detect.** If the tool returns `Error`, surface the error — do not guess.
- **Stay within the 4 cadence types.** Don't introduce "minor plagal" or "Phrygian half" — those are real but not in this catalog and would confuse the explanation.

## What this skill does NOT do

- **Reharmonization** — replacing existing chords with non-diatonic substitutes. Defer to `chord-substitution`.
- **Modal cadences** (Phrygian half, Lydian II–I, etc.) — out of scope for this skill.
- **Voice leading** — the tool returns chord symbols, not specific voicings or fingerings.
- **Style-specific suggestions** ("what comes next in a jazz / metal / bluegrass tune?") — this skill is style-neutral; the cadence catalog is the universal harmonic vocabulary.

## Cross-reference

- Reused MCP tool: `Common/GA.Business.ML/Agents/Mcp/KeyIdentificationMcpTools.cs` — same tool the `key-identification` skill uses.
- Legacy C# skill it replaces: `Common/GA.Business.ML/Agents/Skills/ProgressionCompletionSkill.cs` (regex-driven + LLM phrasing — kept for the deterministic fast path)
- Sibling skill: `skills/key-identification/SKILL.md` for "what key is X" queries that don't ask for a continuation.
