---
name: "diatonic-chords"
description: "Lists the diatonic chords (triads or sevenths) for a given key — e.g. C major: C Dm Em F G Am Bdim. Calls the deterministic `ga_diatonic_chords` MCP tool. Use when a learner asks 'what chords are in X major' / 'diatonic chords in Y minor' / 'chord scale of Z'."
triggers:
  - "diatonic chord"
  - "diatonic chords"
  - "chord scale"
  - "chords in the key"
  - "chords belong to"
  - "what chords are in"
  - "i ii iii iv v vi vii"
  - "harmonized scale"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 1 daily-use skill (skill-stewards 2026-05-05)"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_diatonic_chords
---

# Diatonic Chords of a Key

Pedagogy staple: when a user asks what chords belong to a key, call `ga_diatonic_chords`. Don't compute by hand — the tool guarantees correct quality (major / minor / diminished) on every degree, including the half-diminished vii° in major and the V chord in minor (typically harmonic-minor V = major dominant).

## Calling the tool

Arguments:

- `key` — string, e.g. `"C major"`, `"A minor"`, `"F# major"`, `"Eb minor"`.
- `chordType` — `"triads"` (default) or `"sevenths"`.

Returns:

- `Key` — echoed back.
- `Chords` — array of 7 chord symbols in scale-degree order.
- `RomanNumerals` — array of 7 Roman numerals (uppercase = major, lowercase = minor, ° = diminished).
- `Notes` — for each chord, the chord tones (optional in compact mode).

## Mapping user phrasings

- *"What chords are in G major?"* → `key="G major", chordType="triads"`.
- *"Diatonic seventh chords in C minor"* → `key="C minor", chordType="sevenths"`.
- *"Chord scale of D Dorian"* — Dorian isn't a major/minor key; explain that diatonic-chords works on major/minor and offer to call `ga_scale_by_name` for the mode's chord scale separately.
- *"Triads of F# minor"* → `key="F# minor", chordType="triads"`.

## Phrasing the answer

Present chord-symbol + Roman numeral pairs, in scale order:

> **G major** diatonic triads:
> - I – G
> - ii – Am
> - iii – Bm
> - IV – C
> - V – D
> - vi – Em
> - vii° – F#°

For seventh chords, follow the same pattern with quality suffixes (Imaj7, ii7, iii7, IVmaj7, V7, vi7, vii⌀7).

## When to clarify

- *"Chords in A"* — major or minor? Ask. Don't assume.
- *"Modal chord scale"* — defer to a future modal-chord-scale skill or use scale-info as a workaround.

## Out of scope

- Modal harmony (Dorian / Phrygian / Lydian / Mixolydian / Aeolian / Locrian chord scales) — separate skill.
- Borrowed / secondary chords — out of scope for this skill; offer `chord-substitution` instead.

## Cross-reference

- MCP tool: `ga_diatonic_chords` (Common/GA.Business.ML/Agents/Mcp/ScaleMcpTools.cs)
