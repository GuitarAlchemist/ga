# GA Research Protocol

*Fundamental research on music and on the GA codebase — done reproducibly.*

This directory is where **studies** live: bounded investigations that ask a real
question, run an experiment against evidence, and reach a verdict we can trust
later. It is deliberately distinct from:

- `docs/plans/` — *what we will build* (commitments).
- `docs/solutions/` — *how we fixed a known bug* (compounded fixes).
- `docs/adr/` — *a decision and its consequences* (architecture).

A study may *inform* a plan, a solution, or an ADR — but a study's job is to
**find out whether something is true**, not to ship it. A study that concludes
"no" is as valuable as one that concludes "yes"; both get committed.

Exemplar / "study 0": [`2026-06-15-nested-loop-chatbot-development-duckdb.md`](2026-06-15-nested-loop-chatbot-development-duckdb.md)
— read it to see the target quality bar (hypothesis-framed, TL;DR up top, one
honest caveat).

---

## The lifecycle

Every study moves through five stages. The template (`_TEMPLATE.md`) has a
section for each.

| Stage | The question it answers | Must produce |
|---|---|---|
| **1. Question** | What don't we know, and who is in pain not knowing it? | One sentence, falsifiable in principle |
| **2. Hypothesis** | What do we expect, and what would prove us *wrong*? | A claim + its refutation condition |
| **3. Method** | How exactly do we test it? | A **reproducible command / tool sequence** anyone can re-run |
| **4. Evidence** | What did the experiment actually produce? | Committed artifacts under `state/research/<slug>/` |
| **5. Verdict** | What do we now believe, and how strongly? | Confidence + who/what validated it |

The non-negotiable is **stage 3**: if a study can't be re-run from what's
written down, it isn't research, it's an anecdote. Prefer a one-liner
(`pwsh Scripts/...`, an `ix_*` call with exact args, a GA MCP tool invocation)
over prose describing what you did.

---

## Stage → tool mapping (this repo)

The point of a *cadre* is that the tools already exist — the protocol just says
which one belongs at which stage. This stack is unusually well-equipped:

**Music-domain evidence**
- **GA MCP** (`ga_*`) — set theory, ICV, voice-leading, voicings, substitutions.
  Turns a theory conjecture into a computed answer instead of a hand-argument.
- **OPTIC-K** (`Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs`) + the ix
  **SAE** artifacts (`state/quality/optick-sae/<date>/`) — the substrate for
  interpretability studies.
- `Tools/GaStructureInvariance` — empirical invariance measurement (transposition /
  voicing / instrument).

**Analysis / ML (ix MCP, ~90 tools)**
- Structure discovery: `ix_pca`, `ix_tsne`, `ix_svd`, `ix_dbscan`, `ix_kmeans`,
  `ix_gmm`, `ix_silhouette`.
- Geometry / topology of the embedding space: `ix_topo`, `ix_category`,
  `ix_grothendieck_path` / `_delta` / `_nearby`, `ix_distance`, `ix_spectral_distance`.
- Signal: `ix_fft`, `ix_wavelet_denoise`, `ix_spectrogram`, `ix_autocorrelation`.
- Grammar / evolution: `ix_grammar_evolve`, `ix_grammar_search`, `ix_evolution`.

**Code-as-object-of-study**
- `ix_code_analyze`, `ix_code_smells`, `ix_git_churn`, `ix_git_log`,
  `ix_ast_query`; **sentrux** (structural quality); **tars** (symbol extraction,
  F# grammar validation, complexity).

**Literature / prior art** (do this *before* stage 2 — check we're not
re-deriving a known result)
- Skill **`/paper-search`** — arXiv, PubMed, Semantic Scholar, CrossRef (20+ sources).
- Skill **`/deep-research`** — fan-out web + adversarial verification + cited report.
- **notebooklm** MCP — RAG grounded on an uploaded corpus of papers.
- `tars__search_semantic_scholar`.

**Verdict validation (stage 5)**
- **Multi-model cross-check** — route an independent verifier at a *different*
  model (`Agent` with `model: "fable"` for Fable 5, or **tars** as the
  cross-model theory validator per ADR-0003: tars checks *consistency, not
  truth*). A claim marked `concluded` should survive at least one independent
  model's attempt to refute it.
- **Epistemic recording** — a concluded belief goes into **hari**
  (`hari_record_observation` / `hari_consensus`) and, for governance-relevant
  claims, **Demerzel** epistemic. That makes "what do we believe, and why?"
  a query across sessions, not tribal memory.

---

## Conventions

**Narrative note** → `docs/research/YYYY-MM-DD-<kebab-slug>.md`, front-matter per
`_TEMPLATE.md`. This is the human-readable study.

**Reproducible artifacts** → `state/research/<slug>/` (committable — *not*
gitignored). Put small structured outputs here (verdict JSON, summary stats,
plot data). For heavy raw dumps (large matrices, full embedding exports) add a
per-study glob to `.gitignore` and commit only the summary — never bloat the
repo with regenerable data.

**Naming** — the slug is shared between the note and its artifact dir so they
grep together (`2026-07-19-optick-sae-feature-atlas` ↔
`state/research/optick-sae-feature-atlas/`).

**Status** lives in front-matter: `open` (question posed, not started) →
`active` (experiment running) → `concluded` (verdict + validation) →
`superseded` (a later study overturned it; link forward).

---

## Rigor rules

1. **Falsifiable or it's not a hypothesis.** Stage 2 must name the observation
   that would make you *abandon* the claim.
2. **Re-runnable or it's not evidence.** Stage 3 is a command, not a story.
3. **Prior art before novelty.** Run `/paper-search` first; cite what exists.
   Re-deriving a 1990 MIR result is fine — *not knowing* you did is not.
4. **Independent verdict.** No study reaches `concluded` on a single model's
   say-so. Cross-check with Fable 5 / tars; record dissent if there is any.
5. **Instrument one-way doors.** If a study's verdict would justify changing
   OPTIC-K dims, a schema, or a public API, it inherits Karpathy rule #6 —
   baseline + direction + guardrail + explicit sign-off. (One-way door reminder:
   OPTIC-K dimension is a coordinated re-index — never a side effect of a study.)

---

## Starting a study

```
cp docs/research/_TEMPLATE.md docs/research/$(date +%Y-%m-%d)-<slug>.md
mkdir -p state/research/<slug>
```

Fill Question + Hypothesis, then work the Method. Update status as you go. On
conclusion, record the belief in hari and (if it moves a metric or a door)
open the follow-up plan/ADR — the study links to it, not the other way around.

## Index of studies

| Date | Study | Status | Verdict (one line) |
|---|---|---|---|
| 2026-07-19 | [OPTIC-K SAE feature atlas](2026-07-19-optick-sae-feature-atlas.md) | concluded | Yes, partially — two disjoint concept classes (92 exact-PC-set + 59 transposition-invariant ICV-quality detectors), heavy feature-splitting, ~half the dict near-dead. 3 Fable 5 passes; top-K misleads both ways |
| 2026-06-15 | [Nested-loop chatbot dev w/ DuckDB](2026-06-15-nested-loop-chatbot-development-duckdb.md) | concluded | Yes — ~80% already built; the oracle, not the loop, is load-bearing |
