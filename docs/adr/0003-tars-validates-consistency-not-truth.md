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

## Consequences

- TARS contradiction output is a **source the existing `qa-verdict` may cite**, not a parallel tribunal.
- Claims are ingested **offline** (piggybacked on the chatbot-qa cycle), so the chatbot response path
  takes no TARS dependency or latency — important: sibling MCP servers (ix observed down) must never gate
  a live answer.
- Only **functional, GA-canonicalizable** predicates are consistency-checkable. Fuzzy/aesthetic claims
  ("rootless sounds warmer") are out of scope by construction — they aren't consistency-checkable anyway.
- Output is a **quality-snapshot envelope** under `state/quality/consistency/` — no new dashboard infra.
