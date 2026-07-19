# Research is a first-class artifact class with a reproducibility protocol

GA is unusually well-equipped for *fundamental* research on both music and its
own source: GA MCP (computable theory), OPTIC-K + the ix SAE (an embedding space
worth interpreting), ~90 ix ML/math tools (PCA/tSNE/topology/category theory/
Grothendieck geometry), `/paper-search` + `/deep-research` for prior art,
notebooklm for grounded literature RAG, and multiple *independent* models
(Opus 4.8, Fable 5, tars) for cross-validation. What was missing was not a
capability — it was a **protocol** that makes an investigation reproducible and
its verdict trustworthy across sessions. Without one, "research" degrades into
anecdotes: a tool was run, a conclusion was asserted, and neither the exact
method nor the independent check survived to the next session.

We formalize **`docs/research/` as a first-class artifact class**, parallel to
`docs/plans/` (commitments), `docs/solutions/` (fixes), and `docs/adr/`
(decisions). A *study* answers "is this true?" — not "should we build it?" — and
a "no" verdict is committed with the same weight as a "yes". The protocol
(`docs/research/README.md`) fixes a five-stage lifecycle (Question → Hypothesis →
Method → Evidence → Verdict) whose load-bearing stage is **Method as a
re-runnable command**, and maps each stage to the tools this repo already has.
Reproducible artifacts live under `state/research/<slug>/` (committable; heavy
regenerable dumps get a per-study `.gitignore` glob). Two rigor rules give the
verdict its teeth: **prior-art-before-novelty** (run `/paper-search` first) and
**independent-verdict** (no study is `concluded` on one model's say-so —
cross-check with Fable 5 / tars, and record the belief in hari so
"what do we believe and why" becomes queryable).

This is intentionally lightweight: a README + a template + a directory + this
ADR. No new tooling, no dashboard, no CI gate — the framework is a *discipline*,
not a system, and stays reversible.

## Considered options

1. **Docs protocol + template, reusing the existing stack — chosen.** Zero new
   infrastructure; grafts onto `docs/research/`, `state/`, hari, and the ix/GA/
   tars MCP tools already present. The exemplar study
   (`2026-06-15-nested-loop-chatbot-development-duckdb.md`) already sets the
   quality bar, so the template just codifies it.
2. **A `/research` scaffolding skill (like `/digest`, `/learnings`) — deferred.**
   Worth adding once the manual protocol has been exercised on 2–3 real studies
   and the friction points are known. Building the skill first would harden a
   shape we haven't validated.
3. **A heavyweight experiment-tracking system (notebooks + run DB + dashboard) —
   rejected.** Over-engineered for the current cadence; violates simplicity-first.
   The DuckDB quality layer already exists if a study *needs* durable metrics.

## Consequences

- Every future fundamental-research effort starts from `_TEMPLATE.md` and lands a
  narrative note + a `state/research/<slug>/` artifact dir sharing one slug.
- `concluded` requires an **independent model/tool** re-check; dissent is
  recorded, not hidden. Fable 5 gets a standing role here as the *diversity*
  voice, not an extra executor.
- Verdicts that would justify a one-way-door change (OPTIC-K dims, schema, public
  API) inherit Karpathy rule #6 sign-off — a study can *recommend* such a change
  but must not enact it inline.
- If the protocol proves friction-heavy in practice, option 2 (the `/research`
  skill) is the next step; if it proves too light, the DuckDB layer is the
  escalation path. Both are additive — this ADR is not a one-way door.
