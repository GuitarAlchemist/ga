# CONTEXT — ga domain glossary

> The shared language of the GuitarAlchemist (ga) repo. `/grill-with-docs` grows
> this lazily as terms get resolved; `/improve-codebase-architecture`, `/diagnose`,
> and `/tdd` read it so their output uses **our** words. This is a **seed** — add
> terms when a real ambiguity is resolved, not speculatively.

## What ga is

The GuitarAlchemist product: a .NET (C#/F#) + React codebase for **music theory**,
**voicings**, and **RAG**-backed chord/scale intelligence, with a chatbot and the
Prime Radiant visualization. Sibling of **ix** (ML/governance), **tars** (grammar),
and **Demerzel** (governance). Cross-repo collaboration is via JSON-on-disk
contracts (see `docs/contracts/`).

## Architecture invariant

**Five-layer model**, strict bottom-up dependency (see `docs/architecture/layers.md`):
Core → Domain → Analysis → AI/ML (layer 4) → Orchestration (layer 5). AI code lives
at layer 4, orchestration at 5; never in lower layers.

## Core terms (seed)

- **Voicing** — a fingered chord shape on the fretboard. Voicing-specific display
  names (e.g. `Dsus4`) live in **`DisplayName`**, not `CanonicalName`.
- **OPTIC-K** — the mmap voicing index schema (built by ix's `ix-voicings`/`ix-optick`,
  consumed here); current schema is v1.8 (`optk-v4-pp-r`).
- **Chord recognizer** — ranking is **PC-set-only**; bass lives only in the slash
  suffix, never in the ranking.
- **Chatbot routing** — **embedding-first** (15s query-embed timeout); don't tune
  skill keyword gates to fix routing.
- **dev-data middleware** — the `/dev-data/*` Vite endpoints powering the dashboard
  (`demos.guitaralchemist.com/test#dev/...`); dev-server-only (stripped by `vite build`).
- **Prime Radiant** — the 3D governance/assumption-graph visualization.
- **Weighted partition cosine** — the OPTIC-K similarity score: `Σ weight[p]·cosine(a[p], b[p])`
  over the similarity partitions of two **raw** vectors. Owned by `EmbeddingSchema`
  (`WeightedPartitionCosine`); equals the dot product of the two **compact** vectors
  (`ExtractCompact`, per-partition L2 × √weight) by construction — corpus build, query encode,
  and the CPU/GPU scorers all cross these layout operations rather than re-deriving offsets/weights.

## Conventions

See `CLAUDE.md` for authoritative build/convention rules and the layer model.
