---
name: "progression-generator"
description: "Generates a chord progression matching a description — mood, genre, key, length, complexity. Calls the deterministic `ga_generate_progression` MCP tool. Use when a learner asks 'generate a melancholy progression in D minor' / 'give me a jazz turnaround in F'."
triggers:
  - "generate a progression"
  - "generate a chord progression"
  - "give me a progression"
  - "create a progression"
  - "make a progression"
  - "write a progression"
  - "come up with a progression"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 3 skill (skill-stewards 2026-05-05)"
  blocked_on: "ga_generate_progression MCP tool — not yet implemented in Common/GA.Business.ML/Agents/Mcp/"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_generate_progression
---

# Generate a Chord Progression

Creative request — when a user asks GA to generate a progression matching a vibe, call `ga_generate_progression`. The tool composes from curated harmonic templates (jazz turnarounds, pop loops, modal vamps, blues forms) rather than relying on LLM creativity, which produces same-y output and dubious voicings.

## Calling the tool

Arguments:

- `mood` — `"sad"` / `"melancholy"` / `"happy"` / `"bright"` / `"tense"` / `"dreamy"` / `"epic"` etc.
- `key` — required, e.g. `"D minor"`, `"C major"`.
- `length` — number of chords, default 4.
- `style` — optional: `"jazz"`, `"pop"`, `"folk"`, `"blues"`, `"modal"`, `"film-score"`.
- `complexity` — `"simple"` (triads), `"intermediate"` (sevenths), `"advanced"` (extensions, alterations).

Returns:

- `Chords` — array of chord symbols.
- `RomanNumerals` — array (so the user can transpose).
- `Style` — echoed.
- `Rationale` — one-sentence why-this-fits-the-mood.

## Mapping user phrasings

- *"Generate a melancholy progression in D minor"* → `mood="melancholy", key="D minor", length=4`.
- *"Jazz ii-V-I in F"* — known template; `mood="standard", style="jazz", key="F major", length=3`.
- *"Bright pop progression in G"* → `mood="bright", style="pop", key="G major", length=4`.
- *"Dreamy modal vamp on Em"* → `mood="dreamy", style="modal", key="E minor", length=2-4`.

## Phrasing the answer

Lead with the chord symbols, then Roman numerals, then a short interpretive line:

> Try this melancholy 4-chord loop in D minor:
>
> **Dm – Bbmaj7 – Gm – A7**
> (i – VI – iv – V)
>
> The VI gives the unexpected lift, the iv is the pull-back into minor territory, and the V (with its leading tone C#) sets up the resolve back to Dm. Classic film-score / Spanish-tinge feel.

## When to refuse / clarify

- *"Generate a song"* — too broad; ask for a section (verse, chorus, bridge) or just a 4–8 chord loop.
- *"In any key"* — pick C major or A minor as default and state the assumption.

## Out of scope

- **Melody generation** — out of scope; this is harmony only.
- **Rhythm / time-feel** — out of scope; tool returns chord symbols, not timing.

## Cross-reference

- MCP tool: `ga_generate_progression` (Common/GA.Business.ML/Agents/Mcp/ProgressionMcpTools.cs)
- Companion: `progression-analysis` to explain a generated progression.
