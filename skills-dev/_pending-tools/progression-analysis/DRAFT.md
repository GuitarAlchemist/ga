---
name: "progression-analysis"
description: "Analyzes a chord progression — identifies key, Roman numerals, cadences, modulations, and harmonic function of each chord. Calls the deterministic `ga_analyze_progression` MCP tool. The single most-requested prompt class on the public chatbot — every theory student asks 'what does this progression do'."
triggers:
  - "analyze this"
  - "analyze the progression"
  - "what does this progression"
  - "analyze these chords"
  - "harmonic analysis"
  - "function of these"
  - "roman numeral analysis"
  - "what's happening in"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 1 daily-use skill (skill-stewards 2026-05-05)"
  blocked_on: "ga_analyze_progression MCP tool — not yet implemented in Common/GA.Business.ML/Agents/Mcp/"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_analyze_progression
  - ga_key_identify
---

# Analyze a Chord Progression

The headline skill for the public chatbot. When a user submits a chord sequence and asks for analysis (Roman numerals, key, cadences, modulations), call `ga_analyze_progression`. This is the single most-requested prompt class on `demos.guitaralchemist.com/chatbot/`.

## Calling the tool

Arguments:

- `chords` — array of chord symbols, e.g. `["C", "Am", "F", "G"]`.
- `assumeKey` — optional; if user names a key, pass it. Otherwise the tool derives via `ga_key_identify`.
- `includeNonDiatonic` — `true` (default) to flag borrowed/secondary chords.

Returns:

- `Key` — detected (or asserted) key.
- `RomanNumerals` — array, one per chord, e.g. `["I", "vi", "IV", "V"]`.
- `Functions` — array: `tonic`, `predominant`, `dominant`, `borrowed`, `secondary-dominant`, `chromatic-mediant`, etc.
- `Cadences` — array of identified cadences (authentic, plagal, half, deceptive) with their position in the progression.
- `Modulations` — array of `{ atIndex, fromKey, toKey, type }` if any.
- `NonDiatonic` — chords that don't belong to the detected key (with explanation).

## Mapping user phrasings

- *"Analyze C Am F G"* → `chords=["C","Am","F","G"]`. Returns I-vi-IV-V in C major.
- *"What does Dm7 G7 Cmaj7 do?"* → ii-V-I in C major (jazz cadence).
- *"Roman numerals for D A Bm G"* → `chords=["D","A","Bm","G"]`. Returns I-V-vi-IV in D major.
- *"Analyze this progression in F minor: Fm Bbm C7 Fm"* → `assumeKey="F minor"`. Returns i-iv-V-i (with V as harmonic-minor major dominant).

## Phrasing the answer

Lead with the **key** and the **Roman numeral string**, then unpack functions and cadences:

> **Key**: C major
> **Roman numerals**: I – vi – IV – V
>
> This is the classic *axis* progression. Functions:
> - **C** (I) → tonic
> - **Am** (vi) → tonic substitute
> - **F** (IV) → predominant
> - **G** (V) → dominant
>
> Ends on a half cadence (V), expecting resolution back to I — typical loop progression in pop and folk.

## When to refuse / clarify

- One or two chords only — defer to `voice-leading` for pair analysis or `chord-info` for a single chord.
- User says *"my song goes [hums]"* — refuse; can't analyze melodic input as a chord progression.
- Progression with unparseable chords — surface the parse error per-chord rather than discarding the whole sequence.

## Out of scope

- **Phrase / form analysis** (verse-chorus, AABA, sonata) — out of scope.
- **Voice-leading optimisation** — defer to `voice-leading` skill (per-pair) or chain it after analysis.
- **Suggesting next chords** — defer to `progression-completion` skill.

## Cross-reference

- MCP tool: `ga_analyze_progression` (Common/GA.Business.ML/Agents/Mcp/ProgressionMcpTools.cs)
- Companion: `ga_key_identify` for key-only queries.
