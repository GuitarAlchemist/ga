---
name: "set-class-subs"
description: "Lists pitch-class set substitutions for a given chord — set-class equivalents, complement, transposition cycles. Calls the deterministic `ga_set_class_subs` MCP tool. Specialist skill for atonal theory, post-tonal composition, and Forte-set analysis."
triggers:
  - "set class"
  - "set-class"
  - "forte"
  - "pitch class set"
  - "pitch-class set"
  - "atonal substitute"
  - "complement set"
  - "transposition class"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 3 specialist skill (skill-stewards 2026-05-05)"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_set_class_subs
  - ga_chord_to_set
---

# Set-Class Substitutions

Specialist atonal-theory skill. When a user asks for set-class substitutes (Forte-set equivalents, T_n/T_nI cycles, complements), call `ga_set_class_subs`. LLMs hallucinate Forte numbers and confuse normal-form / prime-form conventions; the tool is deterministic.

## Calling the tool

Arguments:

- `chord` — chord symbol OR pitch-class set notation, e.g. `"Cmaj7"` or `"[0,4,7,11]"` or `"4-20"` (Forte number).
- `relations` — array of `["transposition", "inversion", "complement", "z-related"]` to include.

Returns:

- `Input` — echoed (parsed to normal form).
- `ForteNumber` — e.g. `"4-20"`.
- `PrimeForm` — e.g. `"(0,1,5,8)"`.
- `Substitutions` — for each relation type, an array of equivalent chords/sets.
- `ZPartner` — non-null if the set has a Z-related complement (different Forte number, same interval-class vector).

## Mapping user phrasings

- *"Set-class substitutes for Cmaj7"* → `chord="Cmaj7", relations=["transposition","inversion"]`.
- *"What's the Forte number of [0,1,5,8]?"* → `chord="[0,1,5,8]"`. Returns 4-Z29 or whichever applies.
- *"Z-related set of 4-15"* → `chord="4-15", relations=["z-related"]`. Returns the Z-partner.
- *"Complement of [0,3,6,9]"* → `chord="[0,3,6,9]", relations=["complement"]`. Returns [1,2,4,5,7,8,10,11].

## Phrasing the answer

Lead with Forte number and prime form, then list the requested substitutions:

> **Cmaj7** = pitch-class set **{0, 4, 7, 11}** = Forte **4-20** (prime form **(0,1,5,8)**).
>
> Transposition cycle (T_n equivalents — same set class):
> - T_1: C#maj7 / Dbmaj7
> - T_2: Dmaj7
> - ...
>
> Inversion cycle (T_nI):
> - I-equivalent: Aminmaj7 (same prime form, different transposition)

## When to refuse / clarify

- User unfamiliar with set-class theory — ask if they want a quick primer first, then defer to `circle-of-fifths` or `chord-substitution` for tonal-music substitutions instead.
- Set notation ambiguity — ask whether they mean normal form, prime form, or pitch classes.

## Out of scope

- **Twelve-tone / serial transformations** (P/I/R/RI rows) — separate skill.
- **Tonal harmonic substitution** (tritone sub, secondary dominant) — defer to `chord-substitution`.

## Cross-reference

- MCP tool: `ga_set_class_subs` (Common/GA.Business.ML/Agents/Mcp/AtonalMcpTools.cs)
- Companion: `ga_chord_to_set` for the chord-to-pitch-set bridge.
