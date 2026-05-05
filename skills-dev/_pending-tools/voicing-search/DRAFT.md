---
name: "voicing-search"
description: "Searches the OPTIC-K voicing corpus by natural-language query — e.g. 'mellow jazz Cm9' / 'bright open-position Em' / 'rootless Dm7'. Calls the deterministic `ga_search_voicings_by_query` MCP tool with embedding-based retrieval over the OPTIC-K corpus. Use when a learner asks for a voicing with a vibe description rather than a structural one."
triggers:
  - "find me a voicing"
  - "voicing for"
  - "find a voicing"
  - "jazz voicing"
  - "mellow voicing"
  - "bright voicing"
  - "rootless"
  - "shell voicing"
  - "drop 2"
  - "drop 3"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "drafted in skills-dev/ as Tier 2 skill (skill-stewards 2026-05-05)"
  blocked_on: "ga_search_voicings_by_query MCP tool — not yet implemented in Common/GA.Business.ML/Agents/Mcp/"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_search_voicings_by_query
---

# Voicing Search by Natural-Language Query

The OPTIC-K corpus (current schema version is read at runtime from EmbeddingSchema.Version; total dimension is EmbeddingSchema.TotalDimension — never hardcode) indexes voicings with structural + perceptual embeddings (STRUCTURE / MORPHOLOGY / CONTEXT / SYMBOLIC / MODAL / ROOT partitions). When a user asks for a voicing with a *style* description ("mellow", "rootless", "jazz", "bright"), call `ga_search_voicings_by_query`. The semantic search outranks LLM-recalled voicings because the corpus is curated for harmonic + ergonomic quality.

## Calling the tool

Arguments:

- `query` — natural-language description, e.g. `"mellow jazz Cm9"`, `"bright open-position Em"`.
- `topK` — number of results, default 5.
- `instrument` — `"guitar"` (default) or other tuning.

Returns:

- `Results` — array of `{ chord, voicingDiagram, similarityScore, contextTags, sourceCorpus }`.
- `QueryEmbedding` — for debugging; can be ignored in user-facing output.

## Mapping user phrasings

- *"Find me a mellow Cm9"* → `query="mellow Cm9"`.
- *"Rootless Dm7 voicing"* → `query="rootless Dm7"`.
- *"Drop-2 voicing for Gmaj7"* → `query="drop 2 Gmaj7"`.
- *"Bright open-position Em"* → `query="bright open Em"`.

## Phrasing the answer

Lead with the top result's diagram, mention similarity score and context tags:

> Top match for *"mellow Cm9"* (similarity 0.91):
>
> ```
> e|--3--   (D — 9th)
> B|--3--   (D)
> G|--3--   (Eb — minor third)
> D|--5--   (G — fifth)
> A|--3--   (C — root)
> E|--x--
> ```
>
> Tagged: `low-density`, `rootless-on-bass`, `quartal-flavour`. Two more options below if this doesn't fit:
> ...

## When to refuse / clarify

- *"Find me a chord"* (no chord named) — ask which chord they want. The tool needs at least an implied chord name in the query.
- Vibe-only queries with no chord (*"give me something jazzy"*) — defer to `progression-generator` instead.

## Out of scope

- **Voicings outside the corpus** — the tool only returns indexed results; novel voicings aren't synthesised.
- **Voicing analysis** of a user-provided diagram — defer to `chord-info` (extract notes) + `fret-span` (playability).

## Cross-reference

- MCP tool: `ga_search_voicings_by_query` (Common/GA.Business.ML/Agents/Mcp/VoicingMcpTools.cs)
- Backing corpus: OPTIC-K (read EmbeddingSchema constants at runtime; the schema version + dimension change without coordinated re-index).
