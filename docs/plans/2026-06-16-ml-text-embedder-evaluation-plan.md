# Text-embedder evaluation & swap — plan

**Date:** 2026-06-16 · **Type:** ml/arch · **Status:** proposed
**Owner:** TBD · **Reversibility:** Phases 0–2 reversible (config); Phase 3 touches persisted stores

## Problem

Across a session of routing-quality work (routing-ambiguity diagnostic, two
example-curation passes, the learned-head assessment), one signal was consistent:
**the text embedder — `nomic-embed-text` (768-d, via Ollama) — is the ceiling**,
not the routing algorithm on top of it.

Evidence:
- Intent example-prompt anchors have **overall silhouette 0.036** — the clouds
  barely separate (`routing-ambiguity-diagnostic`). Curation lifted individual
  clusters but the global number didn't move: the embedding geometry is the limit.
- A learned softmax head **does not beat the cosine router** at current scale
  (deployable head 0.904 vs production 0.934) — adding model capacity on top of
  nomic embeddings doesn't help; the embeddings themselves are the constraint
  (see `docs/runbooks/router-learned-head-shadow.md`).
- Production in-scope routing accuracy sits at **0.934** with curated
  descriptions + hint rules compensating for weak separation.

A stronger text embedder is the one lever that improves routing/retrieval
**without** needing more labeled data (it imports knowledge from a better
model's pretraining) — unlike fine-tuning / adapters / learned heads, all of
which are gated on the 166-prompt corpus.

## Goal & success criteria

Pick and (if it wins) ship a better text embedder for **semantic intent
routing**, measured, with no regression and minimal blast radius.

Success = a candidate that, vs the nomic baseline:
- **routing-eval in-scope accuracy** clears the Phase-3 ratchet margin (#412)
  above 0.934 — the load-bearing metric;
- **intent-anchor silhouette** materially above 0.036 (geometry actually improved);
- **OOS-decline rate** held (≥ current 0.875) — doesn't start force-routing junk;
- **per-route latency** acceptable for the chat path (embed 1 query + cached anchors).

Non-goal (this plan): changing the OPTIC-K *musical* voicing embedding
(`optick.index`) — that is a separate 240-d custom embedding and a documented
one-way door; it is **unaffected** by a text-embedder swap.

## Key architectural finding (reshapes the approach)

Routing and the persisted memory store **share one global
`IEmbeddingGenerator<string,Embedding<float>>` singleton** — there is no
per-purpose factory (unlike `IChatClientFactory.Create(purpose)` for chat). So a
naive global swap to a different-dimension model (768→1024) would:
- degrade `MemoryStore` (`~/.ga/memory.json` keeps 768-d vectors; mixed dims make
  cosine scores incomparable until re-embedded);
- **break** `GpuAcceleratedEmbeddingService` (hardcodes `768` at 4 sites);
- break a MongoDB vector index if one was built at 768-d;
- need test-fixture + `/health` constant updates.

**Therefore: decouple the routing embedder before swapping.** Routing has NO
persistence (re-embeds anchors in-memory at startup), so a routing-only swap
behind a purpose factory is **cheap and instantly reversible** and sidesteps
every item above.

## Plan (phased)

### Phase 0 — Bake-off harness (measure first; reversible; ~1 day)
Build an embedder benchmark that, per candidate, reports routing-eval in-scope
accuracy + OOS-decline + intent-anchor silhouette + p50/p95 embed latency.
**Reuse existing infra — all already embedder-parameterized:**
- `RoutingEvalHarness` honours `GA_EMBED_MODEL` / `GA_EMBED_ENDPOINT` → accuracy + OOS.
- `routing-ambiguity-diagnostic` (PR #416) → anchor silhouette per embedder.
- `train-router-head.py` (PR #419) → head-vs-cosine per embedder (headroom signal).

Candidates (local Ollama, pull required — only `nomic-embed-text` is present today):
| model | dim | note |
|---|---|---|
| `nomic-embed-text` | 768 | baseline |
| `mxbai-embed-large` | 1024 | strong MTEB, Ollama-native |
| `bge-large` (`bge-large-en-v1.5`) | 1024 | strong retrieval baseline |
| `snowflake-arctic-embed2` | 1024 | newer, multilingual |
| *(optional)* OpenAI `text-embedding-3-large` / Voyage `voyage-3` | 3072 / 1024 | API — cost + sends queries off-box; only if on-prem isn't required |

Deliverable: a one-shot `Scripts/embedder-bakeoff.ps1` that loops candidates and
writes `state/quality/embedder-bakeoff/<date>.md` (accuracy / silhouette /
latency table). **Decision gate:** does any candidate beat 0.934 by a clear margin?

### Phase 1 — Decouple the routing embedder (enabling; reversible; ~1 day)
Introduce an embedding **purpose** factory mirroring `IChatClientFactory`:
`IEmbeddingGeneratorFactory.Create("routing" | "memory" | …)`. Register a
"routing" instance (config: `AI:Embedding:Routing:Model`) and keep "memory"/default
on nomic-768. Point `SemanticIntentRouter` at the "routing" purpose. No persisted
store changes; default behaviour unchanged when both purposes = nomic.

### Phase 2 — Swap routing to the bake-off winner (reversible; ~½ day)
Set the "routing" purpose to the winner. Routing re-embeds anchors at startup
(no migration). **Gate on the routing-eval ratchet (#412)** — must clear 0.934.
Roll back = config revert + restart. Re-run the routing-ambiguity diagnostic to
confirm the silhouette improved (and re-curate any *new* overlaps the stronger
embedder exposes).

### Phase 3 — Optional global swap / re-embed (only if Phase 0 shows store gains)
If the winner also improves memory/RAG retrieval enough to justify it:
- fix the 768 hardcodes: `GpuAcceleratedEmbeddingService` (×4),
  `VectorSearchService` dim logic, `GA.AI.Service /health` constant, test fixtures;
- idempotent backfill to re-embed `memory.json` (null-embedding entries already
  fall back to BM25, so it's safe + resumable);
- rebuild the MongoDB vector index at the new dim (if used).
Medium risk, persisted-store migration — do **only** with measured justification.

### Phase 4 — Data-gated fine-tune / adapter (much later)
Once shadow (`RoutingShadowLog`) + telemetry have grown the labeled set ~5–10×, a
contrastive fine-tune or a light linear adapter on the *winning* embedder may beat
off-the-shelf. Ties directly to the learned-head finding: learning needs data; the
embedder swap is the data-free win to take first.

## Costs / risks / one-way doors

- **Reversible:** Phases 0–2 are config + a new factory; no persisted re-embed. Phase 3 is the only migration (and is gated + idempotent).
- **Latency:** 1024-d models are slower per embed; routing cost ≈ 1 query embed/route (anchors cached) — measure in Phase 0, keep under the chat-path budget.
- **Privacy/cost:** API embedders send queries off-box and cost per call — prefer local Ollama unless on-prem isn't a constraint; flagged, not assumed.
- **Not a one-way door** for routing (anchors re-embedded fresh). The only one-way-door-adjacent surface is OPTIC-K — explicitly **out of scope**.
- **Dependency:** Phase 0 needs the candidate models pulled (`ollama pull …`) + ~GB disk each.

## Open decisions (need owner input)
1. On-prem only (local Ollama) or are API embedders (OpenAI/Voyage) acceptable for routing?
2. Is the latency budget for a single routing embed call documented anywhere, or should Phase 0 establish it?
3. Build the purpose-factory (Phase 1) eagerly, or only if Phase 0 picks a winner that differs from the memory-store embedder?

## Backlog
Add under an H2 epic in `BACKLOG.md`: "Text-embedder evaluation (routing ceiling)".
First actionable: Phase 0 bake-off harness.
