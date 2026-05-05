---
name: "common-tones"
description: "Lists the notes shared between two or more chords — pivot tones useful for smooth voice leading, pedal points, and harmonic preparation. Calls the deterministic `ga_common_tones` MCP tool. Use when a learner asks 'what notes do X and Y share' / 'common tones between' / 'pivot tone'."
triggers:
  - "common tones"
  - "common tone"
  - "shared notes"
  - "what do x and y share"
  - "pivot tone"
  - "pivot chord"
  - "notes in common"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 2 skill (skill-stewards 2026-05-05)"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_common_tones
---

# Common Tones Between Chords

When a user asks what notes two (or more) chords share, call `ga_common_tones`. Foundational for voice-leading and modulation pedagogy. The LLM can compute this for triads but fumbles on 7ths, 9ths, and altered chords.

## Calling the tool

Arguments:

- `chords` — array of chord symbols, minimum 2, e.g. `["Cmaj7", "Am7"]`.

Returns:

- `Chords` — echoed array.
- `CommonTones` — array of notes present in ALL input chords.
- `PartialCommonTones` — array of `{ note, chordsWithNote }` showing notes shared by some-but-not-all (useful for >2 chord queries).

## Mapping user phrasings

- *"What notes do Cmaj7 and Am7 share?"* → `chords=["Cmaj7","Am7"]`. Returns C, E, G.
- *"Common tones between G and D7"* → `chords=["G","D7"]`. Returns D.
- *"What's shared by C, F, and G?"* → `chords=["C","F","G"]`. Returns C and G (in 2 of 3) — clarify there are no all-three common tones.

## Phrasing the answer

Lead with the count, list the tones, then give a one-line interpretive hook:

> **Cmaj7** and **Am7** share **3 common tones**: C, E, G. That's why Am7 is the relative-vi substitute for C — they're so harmonically close that swapping them barely shifts the colour.

For >2 chords with no full-overlap:

> No tones common to all three. But **C** appears in {C, F} and **G** appears in {C, G} — useful pivots for partial preparation.

## When to refuse / clarify

- One chord — call `chord-info` instead (notes of a single chord).
- Chords with unparseable symbols — flag per-chord parse errors before computing the intersection.

## Out of scope

- **Voice-leading optimisation** — defer to `voice-leading` skill which uses common tones as a sub-step.
- **Modulation suggestions via common tones** — separate skill candidate.

## Cross-reference

- MCP tool: `ga_common_tones` (Common/GA.Business.ML/Agents/Mcp/HarmonyMcpTools.cs)
- Companion: `voice-leading` for the smoothness-optimisation framing.
