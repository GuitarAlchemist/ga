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
- **Governance gate** — the single authority answering "may this loop/skill/overseer
  act right now?", owned by `Scripts/Governance.psm1` (`Test-GovernanceGate`). Folds the
  cross-repo HALT-ALL marker (`~/.demerzel/HALT-ALL`) and the per-repo
  `state/.loop-halted` kill switch into one fail-closed verdict, honoring every
  obligation in the overseer-halt-marker contract (unknown `schema_version` →
  halt; expired → fall through; `exempt_agents`; `scope`). Built on **`Test-Contract`**
  (same module) — the reusable "validate JSON against `docs/contracts/*.schema.json`"
  primitive. Consumers (the loop skills, `dev-process-overseer.ps1`) cross this seam
  rather than re-parsing the marker.
- **Weighted partition cosine** — the OPTIC-K similarity score: `Σ weight[p]·cosine(a[p], b[p])`
  over the similarity partitions of two **raw** vectors. Owned by `EmbeddingSchema`
  (`WeightedPartitionCosine`); equals the dot product of the two **compact** vectors
  (`ExtractCompact`, per-partition L2 × √weight) by construction — corpus build, query encode,
  and the CPU/GPU scorers all cross these layout operations rather than re-deriving offsets/weights.
- **Voicing search strategy** — an interchangeable retrieval backend behind `IVoicingSearchStrategy`.
  Three exist: **OPTK-mmap** (`OptickSearchStrategy`) is the **production default** whenever the index
  file is present; **CPU-Parallel** and **GPU** (ILGPU) are the fallback / explicit-opt-in pair. They
  are *not* equivalent — see *metadata-filter parity*.
- **Metadata-filter parity** — the invariant that the **CPU and GPU** strategies admit or reject the
  *same set* of voicings for a given `VoicingSearchFilters` (the metadata *predicate* agrees); scoring
  and ranking may legitimately differ (`TextEmbedding` vs `Embedding`, symbolic boosting). Owned by the
  shared `VoicingFilterEngine`, the single metadata predicate both cross. **Scoped to CPU↔GPU by
  design:** OPTK-mmap honors only the filters its index actually carries (chord-quality, instrument,
  MIDI range) and is *excluded* from parity — its reduced filter set is **index-bound, not a bug**. A
  strategy declares what it cannot honor via `IVoicingSearchStrategy` so the gap is observable
  (telemetry `dropped`), never silent. See `docs/adr/0002-voicing-filter-parity-cpu-gpu-only.md`.
- **Metadata filter vs comfort filter** — two filter seams with **opposite unknown-bias**, deliberately.
  A **metadata filter** reads a stored attribute and is *strict*: a voicing missing the attribute
  **fails** the filter (`VoicingFilterEngine`). A **comfort filter** (`MinComfortScore`,
  `MustBeErgonomic`) is *analysis-derived* from the diagram via the biomechanical analyzer and is
  *lenient*: a voicing whose diagram can't be parsed **passes** (don't punish what we couldn't
  analyze). The comfort predicate is a shared seam every strategy crosses, not a GPU-only step.
- **Prime form vs normal form (atonal)** — two different canonicalizations, often confused. **Prime
  form** = the minimal-packed-id representative, owned by `PitchClassSetId`: `PrimeForm` folds in
  transposition *and* inversion (the OPTIC / set-class representative, what `PitchClassSet.PrimeForm`
  and `SetClass` use); `TranspositionPrimeForm` folds in transposition only (the OPTC / "Tn-type"
  representative, what `TranspositionClass` uses). **Normal form** (`PitchClassSet.ToNormalForm`) is a
  *separate* idea — canonicalize by interval-span compactness, which can pick a different rotation than
  minimal id. The elementary id ops (complement, inverse, M5/M7, transpose) also live on
  `PitchClassSetId` — it is the single canonicalization authority; the value types delegate.

- **Quality snapshot envelope** — the canonical dashboard tile shape
  (`domain`, `emitted_at`, `metric_name`, `metric_value`, `oracle_status`, `summary`;
  `docs/contracts/quality-snapshot.schema.json`, owned by ix, vendored here). Producers
  MAY add domain-specific fields. **Snapshot registry**
  (`state/quality/.snapshot-registry.json`) is the **opt-in** seam declaring which
  `state/quality/<domain>/` dirs emit envelopes; `Scripts/validate-quality-snapshots.ps1`
  validates only those (via `Test-Contract`), so baselines, `SCHEMA.json`, SAE artifacts,
  and lens sidecars are excluded by design rather than false-failing. A `FAIL` is a real
  producer gap (not yet emitting the envelope), not noise.

## Conventions

See `CLAUDE.md` for authoritative build/convention rules and the layer model.
