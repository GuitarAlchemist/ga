---
name: "icv-neighbors"
description: "Finds chords with similar interval-class vectors (ICVs) to a given chord — neighbours in the Grothendieck-ICV space used for set-class similarity ranking. Calls the deterministic `ga_icv_neighbors` MCP tool. Specialist skill for post-tonal analysis and harmonic-similarity browsing."
triggers:
  - "icv neighbor"
  - "interval class vector"
  - "icv"
  - "harmonically similar"
  - "set class neighbor"
  - "grothendieck"
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
  - ga_icv_neighbors
---

# Interval-Class-Vector Neighbours

Specialist post-tonal skill. When a user asks for harmonically similar chords (in the Grothendieck-ICV sense, not the listening-similarity sense), call `ga_icv_neighbors`. The tool computes ICV distance and returns ranked neighbours — a calculation the LLM cannot reliably reproduce.

## Calling the tool

Arguments:

- `chord` — chord symbol or pitch-class set, e.g. `"Cmaj7"` or `"[0,4,7,11]"`.
- `topK` — number of neighbours, default 5.
- `metric` — `"euclidean"` (default), `"manhattan"`, or `"cosine"` over the ICV.

Returns:

- `Source` — echoed.
- `SourceICV` — the input's interval-class vector, e.g. `[1,1,1,1,2,0]` (entries = count of i.c. 1, 2, 3, 4, 5, 6).
- `Neighbours` — ranked array of `{ chord, icv, distance, forteNumber }`.

## Mapping user phrasings

- *"Harmonically similar chords to Cmaj7"* → `chord="Cmaj7", topK=5`.
- *"ICV neighbours of [0,3,6,9]"* → `chord="[0,3,6,9]"` (the diminished-seventh cycle).
- *"What's close to a half-diminished chord?"* → `chord="C∅7"` (or `"Cm7b5"`).

## Phrasing the answer

Lead with the source ICV, then list neighbours with their distance:

> **Cmaj7** has ICV **[1, 0, 1, 2, 1, 0]** (one i.c.1, zero i.c.2, one i.c.3, two i.c.4, one i.c.5, zero i.c.6 = no tritones).
>
> Top 3 ICV-neighbours (Euclidean distance):
> 1. **Am7** — ICV [1,0,1,2,1,0] — distance 0.0 (same set class, transposition).
> 2. **Cm9 (no root)** — distance 0.6.
> 3. **Fmaj7** — distance 0.0 (transposition by P4).

## When to refuse / clarify

- User unfamiliar with ICV concept — offer a one-line definition: *"The ICV counts how many of each interval class (1=semitone, 2=whole-tone, 3=minor-3rd, 4=major-3rd, 5=perfect-4th, 6=tritone) the chord contains. Two chords with the same ICV sound 'similar' in a structural sense even if their notes differ."*
- Confusion with embedding-similarity — clarify that ICV is structural (Forte-style), while OPTIC-K embedding similarity is perceptual+structural — they often agree but can diverge.

## Out of scope

- **Listening-similarity** ranking — that's OPTIC-K embedding territory; defer to `voicing-search`.
- **Voice-leading distance** between the chords — defer to `voice-leading`.

## Cross-reference

- MCP tool: `ga_icv_neighbors` (Common/GA.Business.ML/Agents/Mcp/AtonalMcpTools.cs)
- Background: Grothendieck-ICV math underpins `chord-substitution` and `set-class-subs`.
