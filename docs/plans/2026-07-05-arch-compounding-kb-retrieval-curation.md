---
title: Compounding knowledge base — retrieval quality + anti-rot curation (implementation plan)
date: 2026-07-05
type: arch (research synthesis → ranked implementation plan)
reversibility: two-way (every step is additive; signals/reports/frontmatter fields, no deletion of existing knowledge)
revisit_trigger: when `docs/solutions/` crosses ~500 entries (currently 36) the vector/hybrid tier moves from "premature" to "earns its keep"; or when a measured recall@k harness shows the agent missing prior solutions it should have found
status: draft plan — grounded in two bounded research passes (2026-07-05), awaiting owner pick of the first slice
one_way_doors: none in slices 1-3; slice 4 (offline compaction) only ever emits a reviewed diff, never mutates originals
---

# Compounding KB — how to best implement retrieval + curation

Karpathy's "LLM Wiki / compounding knowledge base" framing prompted the question *"comment
implementer au mieux"*. This ecosystem **already is** a compounding KB — the honest job is not to
build one but to (a) make the agent actually *find* what's in it before re-solving, and (b) keep it
from rotting. Two bounded research passes (retrieval + curation/anti-rot, ~158k subagent tokens,
reachable sources only, anti-fabrication) fed this plan. Where a claim rests on a search snippet
rather than a fetched page, that is marked below and in the source reports.

## Frame the problem (who is in pain, what changes)

The agent (and the human directing it) re-derives fixes that a `docs/solutions/` entry, a plan, or a
digest already recorded — because *searching frontmatter* (what `learnings-researcher` does today)
misses the entry when the query wording differs from the doc's wording, and because nothing flags an
entry as stale when the code it describes has moved on. The compounding promise ("never solve the
same thing twice") only pays out if retrieval is reliable **and** the store stays trustworthy.

## What the ecosystem already has (do NOT rebuild)

| Capability | Where it lives | Gap it leaves |
|---|---|---|
| Capture surprises → markdown w/ frontmatter (`module`/`tags`/`problem_type`) | `/learnings` → `docs/solutions/` (36 entries today) | no freshness/verification signal on entries |
| Frontmatter-metadata search over past solutions | `learnings-researcher` agent | keyword/wording misses; no ranked full-text |
| Corpus indexer → `catalog.jsonl` | `ix-streeling` (ix repo) | index exists; **no BM25/semantic rank surfaced as a query the agent runs** |
| Session state / cursor / hypotheses | `/digest` → `state/digests/latest.md` | — (working) |
| Plans & one-way-door log | `docs/plans/`, `docs/archive/` | — (working) |
| Quality baselines (propose-only reports) | `state/quality/` daily snapshots | the pattern to reuse for curation reports |

**Key scale fact:** 36 solution docs is *small*. Reachable guidance (Obsidian-semantic-search
snippet) puts the "hybrid vector retrieval earns its keep" threshold at **500–5,000 notes**. Below
~500, BM25/grep over the existing catalog is typically sufficient. So the near-term gap is **wiring
+ anti-rot**, not a vector database. Building vector search now would be future-proofing the Karpathy
R2/aihero-simplicity rules tell us to skip.

## The genuine gaps (ranked by fit-per-effort)

### Slice 1 — retrieve-before-re-solve, backed by BM25 over the existing catalog *(smallest end-to-end tracer)*
The highest-leverage move and the true tracer bullet: it touches every layer (indexer → query → agent
procedure) in one thin slice, extends `ix-streeling`'s `catalog.jsonl` instead of replacing it, and
needs **no embedding model or re-index pipeline**.

- **Rank:** add BM25/full-text ranking over `catalog.jsonl`, with frontmatter (`module`/`tags`/
  `problem_type`) as a hard **pre-filter** (WHERE clause), BM25 ranking *within* the filtered set.
  Reachable engines that fit a Rust indexer: **tantivy** (Lucene-class BM25, <10ms startup —
  README fetched) for a real inverted index, or **SQLite FTS5** (BM25 built-in) to keep it in one
  embeddable file.
- **Chunking:** split on H2/H3 and **prepend the frontmatter (title + module/tags/problem_type) to
  each unit** — worth ~5–10pp per the chunking snippets, and cheap. At current file sizes whole-doc
  chunking is also defensible; the win is carrying frontmatter into whatever unit gets indexed.
- **Wire:** expose the ranked query as a **skill/MCP `search` tool the agent invokes before
  implementation** — mirroring the reachable `MCP-Markdown-RAG` shape (`index_documents` + `search`,
  README fetched). CLAUDE.md already *mandates* "check `docs/solutions/` first"; today that's a hope,
  not a tool. This makes the mandated procedure mechanical.
- **Measure:** a small hand-labeled **recall@k / MRR** harness under `state/quality/` — a set of
  (query → known prior doc) pairs, asserting the doc lands in top-k. Deterministic, no LLM judge —
  fits the ecosystem's oracle discipline better than RAGAS-style LLM-graded metrics. This is the
  instrument that proves the slice worked (Karpathy R6 / "instrument before you ship").

### Slice 2 — staleness signal in frontmatter + CI flag *(lowest risk, pure signal)*
The single lowest-risk anti-rot step, and it matches patterns already run (DESIGN.md sync check,
readme-drift snapshots).

- Add `last_verified: <date>` (+ optional `ttl_days`) to `docs/solutions/` frontmatter. Reachable
  docs-as-code convention (Dosu freshness snippet: `ttl_days: 90` for quickstarts, `365` for
  architecture) + git-metadata age.
- CI check flags any entry past `last_verified + ttl_days` as **stale** — a *signal, never a
  deletion*. Anthropic's own Memory-tool docs endorse age-based expiry, but note: that's for an
  *agent's private scratch memory* where loss is cheap. A human-authored compounding KB is not that —
  so we flag, we don't delete.

### Slice 3 — near-duplicate *report* (propose-only) *(proven technique, human merges)*
Nightly MinHash-Jaccard (threshold ~0.8, `num_perm=128`) over the markdown, writing candidate dup
pairs to `state/quality/`. Decades-old, boring, well-understood IR — not agent-memory hype. At a few
hundred files you don't even need LSH; a plain pairwise Jaccard runs in milliseconds. Optional second
pass: embedding-cosine (reuse existing OPTIC-K/embedding infra) to catch *semantically* reworded
dupes MinHash misses. **Output is a review queue; a human merges. Never auto-merge** (the technique
is explicitly approximate — false positives/negatives by design).

### Slice 4 — offline compaction that emits a *new* reviewed digest *(highest capability, do last)*
Only after 1–3. Copy **Anthropic Dreams' never-mutate-input contract** (well-sourced via multiple
secondary snippets; the feature itself is research-preview, not GA): read the store + transcripts,
emit a **new proposed** consolidated digest (duplicates merged, stale/contradicted entries flagged),
leaving originals untouched — accepted only as a diff a human approves. **Explicitly reject** the
open-source `dream-skill`'s in-place mutation (its README: *"modifies existing memory files"*) — that
is the anti-pattern for a KB we care about. Contradiction detection (Graphiti's mechanism ported
without the graph: embed → retrieve top-k → LLM flags "contradicts entry X") can feed this queue, but
is LLM-noisy → strictly propose-for-review.

## The through-line (the design constraint every careful source agrees on)

**Consolidation is offline, non-destructive, and reviewed; only low-stakes agent-scratch memory gets
auto-deleted.** An append-mostly, human-authored KB sits on the "propose, preserve history, human
accepts" side of that line. Every slice here respects it: signals and reports, never silent edits or
deletions. This is also why the plan is fully two-way-door reversible.

## If we implement one thing first

**Slice 1 (BM25 retrieve-before-re-solve over the existing catalog + a recall@k harness).** It's the
true tracer bullet — end-to-end through indexer, query, and agent-procedure layers; it directly fixes
the "searched frontmatter, missed the wording" failure; it reuses `ix-streeling`/`catalog.jsonl` and
`learnings-researcher` rather than adding infra; and the recall@k harness gives us the baseline to
prove any later vector tier (Slice 1.5, deferred until ~500 docs) actually beats plain BM25 before we
pay for it. Slice 2 is the natural fast-follow (a few frontmatter fields + one CI check) because it's
near-zero-risk and independently valuable.

## Cost of not curating (honest maturity note)

Widely asserted across RAG-ops write-ups ("stale KB ≈ stale training cutoff"; "staleness cascade"),
but **weakly evidenced in reachable sources** — the one number found ("up to 20% retrieval-accuracy
drop from outdated embeddings") is a single unverified secondary claim, page 403'd. Treat the
*mechanism* as credible and the *magnitude* as unproven. This is itself an argument for the recall@k
harness: measure our own drift rather than import someone's number.

## External validation (2026-07-06)

Independent convergence on Slice 1's "filter-first, cosine-as-fallback" stance: the Towards
Data Science *Enterprise Document Intelligence* series (**"Cosine Is Not the Foundation"** /
**"Retrieval Is Filtering, Not Search"**) argues that RAG retrieval is *filtering on structured
fields*, with embeddings an **optional fallback, not the foundation** — the same conclusion this
plan's retrieval pass reached from different sources. Two independent lines landing on filter-first
is evidence the Slice-1 ordering (frontmatter hard-filter → BM25 → vector only later) is right.
**Caveat — OPTIC-K is exempt:** "cosine is not the foundation" is about *generic text* retrieval.
OPTIC-K's `WeightedPartitionCosine` is a hand-engineered 240-dim musical embedding where
partition-weighted cosine *is* the validated design, not a text-cosine crutch — do not let the
headline trigger a rethink of the voicing-retrieval path. (Snippet-sourced; TDS primary 403'd.)

**Mechanistic evidence for the `revisit_trigger` (~500-doc vector threshold):** TDS *"HNSW at
Scale: Why Your RAG System Gets Worse as the Vector Database Grows"* reports that HNSW index recall
*degrades* as the corpus grows — i.e. jumping to a vector DB isn't just premature at 36 docs, it
carries a concrete failure mode at scale. This strengthens the deferral: BM25+filter first, add the
vector tier only when a measured recall@k gap justifies eating that cost/complexity. (Snippet-sourced.)

## Source reports (this session, reachable-sourced)

- Retrieval pass — BM25 vs vector vs hybrid, frontmatter-aware retrieval, chunking, retrieve-before-
  re-solve, recall@k. Fetched verbatim: tantivy, sqlite-vec, LanceDB, MCP-Markdown-RAG READMEs.
- Curation/anti-rot pass — MinHash/SimHash near-dup, ttl frontmatter freshness, Dreams / dream-skill /
  mem0 / Letta compaction, Graphiti contradiction invalidation, HITL line. Fetched verbatim:
  Anthropic Memory-tool docs, Graphiti README, dream-skill README.

Both reports list their 403'd pages and snippet-only claims explicitly; nothing here is cited from an
unread URL.
