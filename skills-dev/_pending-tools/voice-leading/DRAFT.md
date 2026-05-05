---
name: "voice-leading"
description: "Computes smooth voice leading between two chords — minimum total semitone movement, with each voice's destination. Calls the deterministic `ga_voice_leading_pair` MCP tool. Use when a learner asks 'how do I move from X to Y smoothly' / 'voice leading between' / 'connect these chords'."
triggers:
  - "voice leading"
  - "voicelead"
  - "smooth move"
  - "connect these chord"
  - "from x to y"
  - "smooth transition"
  - "minimum movement"
  - "common tone"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 1 daily-use skill (skill-stewards 2026-05-05)"
  blocked_on: "ga_voice_leading_pair MCP tool — not yet implemented in Common/GA.Business.ML/Agents/Mcp/"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_voice_leading_pair
---

# Voice Leading Between Two Chords

When a user asks how to move smoothly from one chord to another, call `ga_voice_leading_pair`. The tool returns a Plücker-line / minimum-displacement solution that the LLM cannot reliably reproduce — voice leading optimisation requires set-class arithmetic, not pattern recall.

## Calling the tool

Arguments:

- `fromChord` — string, e.g. `"Cmaj7"`, `"Dm7"`.
- `toChord`   — string, e.g. `"G7"`, `"Am"`.
- `optimize` — `"minimum_movement"` (default) or `"common_tones"`.

Returns:

- `From` / `To` — chord symbols echoed.
- `VoiceMovements` — array of `{ fromNote, toNote, semitones }` per voice.
- `TotalSemitones` — sum of |movements|; lower = smoother.
- `CommonTones` — notes shared between both chords (move zero).

## Mapping user phrasings

- *"Voice leading from Cmaj7 to Fmaj7"* → `fromChord="Cmaj7", toChord="Fmaj7"`.
- *"How do I smoothly connect Dm7 to G7?"* → `fromChord="Dm7", toChord="G7"`.
- *"What common tones do C and Am share?"* → `optimize="common_tones"` (or use the `common-tones` skill instead — that's the cleaner dispatch).

## Phrasing the answer

Lead with the total movement (the headline number), then the per-voice mapping:

> Smoothest voicing from **Dm7 → G7**: total movement = **2 semitones**.
> - D → D (common tone, 0)
> - F → F (common tone, 0)
> - A → G (down 2 semitones)
> - C → B (down 1 semitone)

## When to refuse / clarify

- Three or more chords — the tool is pairwise. Suggest the user ask one pair at a time, or defer to `progression-analysis` for whole sequences.
- Voicings outside the catalog (e.g. quartal voicings of obscure altered chords) — flag that the tool may return an empty result.

## Out of scope

- **Three-chord voice leading** (e.g. ii-V-I as one optimisation) — pairwise only.
- **Specific instrument constraints** (e.g. guitar fret span) — defer to the `fret-span` skill after.

## Cross-reference

- MCP tool: `ga_voice_leading_pair` (Common/GA.Business.ML/Agents/Mcp/HarmonyMcpTools.cs)
