---
name: "modes"
description: "Lists the seven modes of the major scale (Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian, Locrian) with their scale-degree formulas and characteristic sound. Pure catalog — same fixed pedagogy whether the user asks for the whole list or a single mode."
triggers:
  # Pro-relevant vocab added 2026-05-05 (audit) — modal harmony
  # / cadence / characteristic-interval are pro idioms.
  - "modes of"
  - "diatonic modes"
  - "major scale modes"
  - "seven modes"
  - "what are the modes"
  - "list the modes"
  - "modal harmony"
  - "modal cadence"
  - "characteristic note"
  - "characteristic interval"
  - "ionian"
  - "dorian"
  - "phrygian"
  - "lydian"
  - "mixolydian"
  - "aeolian"
  - "locrian"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "deterministic-catalog"
  origin: "ported from Common/GA.Business.ML/Agents/Skills/ModesSkill.cs — third pure-catalog SKILL.md, joins beginner-chords and progression-mood"
  evidence-kinds:
    - catalog_lookup
---

# The Seven Modes of the Major Scale

Reproduce the catalog below verbatim when the user asks for *the modes* / *the diatonic modes* / *the modes of the major scale*. When the user asks about a single mode (e.g. *"what is Lydian?"*, *"explain Phrygian"*), pull just that row from the table and lead with it before optionally offering the full list.

## Setup

The major scale has 7 modes. Each starts on a successive degree of the parent scale, rotating the same step pattern:

> **Ionian formula: `W-W-H-W-W-W-H`** *(W = whole step, H = half step)*

## The catalog

| # | Mode | Degrees | Character |
|---|---|---|---|
| 1 | **Ionian** | `1 2 3 4 5 6 7` | Bright, the major scale itself |
| 2 | **Dorian** | `1 2 b3 4 5 6 b7` | Minor with raised 6th — jazz/folk staple |
| 3 | **Phrygian** | `1 b2 b3 4 5 b6 b7` | Dark minor with flat 2 — Spanish/flamenco |
| 4 | **Lydian** | `1 2 3 #4 5 6 7` | Major with raised 4th — floating, dreamy |
| 5 | **Mixolydian** | `1 2 3 4 5 6 b7` | Major with flat 7 — bluesy/rock |
| 6 | **Aeolian** | `1 2 b3 4 5 b6 b7` | Natural minor |
| 7 | **Locrian** | `1 b2 b3 4 b5 b6 b7` | Half-diminished — rare as a tonic |

## Mnemonic

*"I Don't Particularly Like Modes A Lot"* — Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian, Locrian.

## Single-mode answer pattern

When the user asks about one mode specifically, lead with: *"**\<Mode>** is mode \<n> of the major scale: degrees `\<formula>` — \<character note>."* Then optionally offer to compare with neighbouring modes (e.g. Dorian vs Aeolian for "minor flavour").

## What this skill does NOT do

These are hard constraints. **Do NOT** answer queries in these categories from this catalog alone.

- **Specific-key note enumeration** (e.g. *"give me the notes of D Dorian"*). This catalog gives Dorian's degree formula but not the resulting notes for any particular root. Defer to the `scale-info` skill or the broader LLM agent path. Do NOT output specific-key note lists from this catalog alone.
- **Non-diatonic modes** (harmonic minor modes, melodic minor modes, modes of limited transposition). This catalog covers only the seven modes of the major (a.k.a. diatonic) scale. If asked, decline cleanly: *"This catalog only covers the diatonic modes; harmonic-minor modes and others would need different tooling."*
- **Composition recommendation** (*"what mode should I use for an angry feel?"*). The character notes here help an LLM phrase a recommendation, but the catalog itself does not prescribe — emit the catalog data, then let the broader agent path reason about fit.

## Cross-reference

- C# implementation: `Common/GA.Business.ML/Agents/Skills/ModesSkill.cs` (regex-driven, kept as deterministic fast path)
- Tests: `Tests/Common/GA.Business.ML.Tests/Unit/ModesSkillTests.cs`
