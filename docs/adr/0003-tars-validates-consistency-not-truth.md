# TARS validates GA chatbot *consistency*, not *truth*

The TARS sibling repo (`../tars/`, "cross-model theory validator") is wired to GA as a **longitudinal
self-consistency oracle**: it ingests the chatbot's music-theory *claims* over time into its knowledge
graph and runs contradiction detection (`graph_find_contradictions`, `temporal_detect_contradictions`).
It answers **"does GA contradict itself?"** — never **"is this claim correct?"** TARS carries no
music-theory ground truth (its contradiction tools are domain-agnostic), so a truth oracle would require
either seeding it from GA's own domain core (circular — it would re-run GA's logic and rubber-stamp it)
or a different LLM answering independently (redundant — the Mistral cross-model agent and the Theory
Tribunal already do cross-model truth-checking). Consistency is the one thing TARS does that GA
structurally **cannot do itself**: detect that the chatbot asserted *X* in one session and *¬X* in
another — a bug class invisible to single-response QA.

A *claim* is a typed fact `{subject, predicate, asserted_value}` with a GA-canonicalized key and the
chatbot's **asserted value stored verbatim** (never compared to a "right answer" — that would make it a
truth oracle). Two contradiction kinds: **intra-response** (the prose asserts a value its own
co-emitted tool call contradicts — fires in one response) and **cross-session** (prose drift over time).
v1 ships one functional predicate, `pitch_classes`, as a tracer bullet validated against the
buggy-era #414 (set-theory comma-extraction) traces. See the GA→TARS claim contract.

## Considered options

1. **Consistency oracle — chosen.** Domain-agnostic contradiction detection over accumulated claims.
   Sound (functional predicates → crisp contradictions), non-circular (never consults GA ground truth),
   and complements the QA Tribunal (single-response) + chatbot-qa (fixed corpus) as the *longitudinal*
   axis.
2. **Truth validation — rejected.** "Is the claim correct?" needs ground truth TARS lacks. Seeding from
   GA is circular; an independent LLM is redundant with existing cross-model checkers and adds a fallible
   model to the trust chain.

## Correction (2026-06-20, same day) — the keystone was wrong

Reading the TARS source (`../tars/v2/src/Tars.Core/BeliefGraph.fs` `findContradictions`,
`TemporalReasoning.fs` `detectContradictions`) **before** writing any producer code revealed that
**TARS does not *detect* contradictions — it *stores and queries* them.** Both tools filter for
relationships already typed `ContradictedBy` / `Contradicts`; neither compares assertion *content*. So the
original premise — "ingest claims and TARS flags same-key-different-value" — is false: the detection must
happen **before** TARS and be asserted into the graph as an edge.

**Corrected architecture:**

- The contradiction **detection** (same `key`, different `asserted_value`) is trivial — a dictionary
  keyed by `key`, flag on mismatch — and lives in **GA**, not TARS.
- The load-bearing, **TARS-independent** component is a GA-side **claim consistency checker**:
  claim-store → detect → emit to `state/quality/consistency/`. Fully buildable/testable with no TARS dep.
- **TARS is demoted to an *optional* downstream ledger**: if/when reachable, GA asserts the
  already-detected contradiction as a `ContradictedBy` edge for cross-repo/temporal persistence + Demerzel
  visibility. Not required for the core value; GA's quality surface + DuckDB already cover most of it.
- Separately, TARS's MCP output layer is **currently broken** (tools return un-awaited `Task` objects,
  e.g. `health_check` → `System.Threading.Tasks.Task\`1[System.Boolean]`) — a `../tars/` fix tracked
  outside GA. Another reason the core design must not depend on it.

The consistency-vs-truth decision above **still holds**; only the *who-detects* assumption changed.

## Consequences

- TARS contradiction output is a **source the existing `qa-verdict` may cite**, not a parallel tribunal.
- Claims are ingested **offline** (piggybacked on the chatbot-qa cycle), so the chatbot response path
  takes no TARS dependency or latency — important: sibling MCP servers (ix observed down) must never gate
  a live answer.
- Only **functional, GA-canonicalizable** predicates are consistency-checkable. Fuzzy/aesthetic claims
  ("rootless sounds warmer") are out of scope by construction — they aren't consistency-checkable anyway.
- Output is a **quality-snapshot envelope** under `state/quality/consistency/` — no new dashboard infra.
