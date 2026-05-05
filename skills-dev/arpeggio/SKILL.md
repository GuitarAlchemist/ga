---
name: "arpeggio"
description: "Suggests arpeggios that work over a single chord or a short progression — for soloing, melody construction, and improvisation. Calls the deterministic `ga_arpeggio_suggestions` MCP tool. Use when a learner asks 'what arpeggios work over X' / 'how do I solo over Y'."
triggers:
  - "arpeggio"
  - "arpeggios"
  - "solo over"
  - "improvise over"
  - "melody over"
  - "arpeggiate"
  - "what notes to play over"
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
  - ga_arpeggio_suggestions
---

# Arpeggio Suggestions for a Chord or Progression

When a user asks what to play over a chord (for soloing, ear training, or melody construction), call `ga_arpeggio_suggestions`. The tool returns arpeggios ranked by harmonic fit — the LLM can't reliably reproduce this ranking without theory drift.

## Calling the tool

Arguments:

- `chord` — single chord symbol (e.g. `"Cmaj7"`), OR an array for a progression.
- `style` — `"standard"` (chord tones only), `"upper-structure"` (extensions / 9, 11, 13), `"jazz"` (includes altered options for dominants).
- `includeFingering` — `true` to return guitar fingering, `false` for note-only.

Returns:

- `Chord(s)` — echoed.
- `Arpeggios` — ranked array of `{ name, notes, fitScore, rationale }`.
- `BestPick` — the highest-fit option with a one-sentence why.

## Mapping user phrasings

- *"What arpeggios work over Cmaj7?"* → `chord="Cmaj7", style="standard"`.
- *"How do I solo over a ii-V-I in C?"* → `chord=["Dm7","G7","Cmaj7"], style="jazz"`.
- *"Upper-structure arpeggios on G7"* → `chord="G7", style="upper-structure"`.
- *"What can I play over an Am chord on guitar?"* → `chord="Am", includeFingering=true`.

## Phrasing the answer

Lead with the **best pick**, then offer alternatives:

> Over **Cmaj7**, the cleanest arpeggio is **C major 7** (C – E – G – B) — direct chord tones.
>
> Other options:
> - **E minor 7** (E – G – B – D) — the relative iii chord; gives a 9th colour.
> - **G major 7** (G – B – D – F#) — adds a 9 (D) and #11 (F#); Lydian colour.

## When to refuse / clarify

- *"Arpeggios for the whole song"* — too broad; ask for the progression or the bar of interest.
- Non-chord harmonic contexts (modal vamps, drone tones) — flag that arpeggio framing may not match modal-soloing intuition.

## Out of scope

- **Scale-based soloing** (modes, blues scale) — defer to a future modal-soloing skill.
- **Fingering optimisation** — defer to `fret-span` after picking the arpeggio.

## Cross-reference

- MCP tool: `ga_arpeggio_suggestions` (Common/GA.Business.ML/Agents/Mcp/SoloingMcpTools.cs)
