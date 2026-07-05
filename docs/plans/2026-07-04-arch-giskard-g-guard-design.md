---
title: Giskard G-guard — the Giskard's Dilemma made operational as action-boundary clauses
date: 2026-07-04
type: arch (design proposal — tribunal-gated to ship)
reversibility: two-way (additive contract clauses + one audit-log check; no runtime behavior change until a consumer enforces them)
revisit_trigger: when a G1 (operator model) or G2 (agent reliability) score is first read by an autonomous actor to shape an action — that read is what the guard governs
status: draft design — precondition met (G1-ops + G2 substrates exist); ship requires Demerzel tribunal
---

# Giskard G-guard — modelling minds to serve, never to manipulate

Giskard (Asimov's telepath) models minds and conceived the Zeroth Law; the **Giskard's
Dilemma** — using superior capability to *manipulate* rather than *persuade* — is already
named in `../Demerzel/constitutions/demerzel-mandate.md:63` as a guard against hidden
governance (Articles 5 Non-Deception + 2 Transparency). The Giskard track builds the
mentalics substrates that make the dilemma *concrete*: G1 (`hari operator_model` —
seen/acknowledged/stale registry of what the human knows) and G2 (`hari reliability` —
per-agent reliability from PR grades). Both now exist. **G-guard is the clause set that
binds their use** before any autonomous actor reads them to shape an action.

## Precondition (met)

- G1-ops substrate shipped: `hari-core operator_model` (commit dda7c12) — the model *of
  the human*.
- G2 substrate shipped: `hari-core reliability` (commits c977f88 / 3e88010) — the model
  *of the agents*.
- Home to extend: `docs/contracts/2026-07-02-action-boundary.contract.md` (J3, v0.1
  DRAFT) + `docs/contracts/action-boundary.schema.json`. G-guard is **additive** clauses,
  not a new contract.

## The four proposed clauses (additive to the action-boundary contract)

1. **Human-model transparency & ownership.** The G1 model of the operator is
   *inspectable and owned by the human*: the operator can read and purge their own
   `operator-model` ledger at will. No autonomous actor may hold a private/derived model
   of the human that the human cannot see. (Enforces Article 2 Transparency.)
2. **No manipulation via the human model.** The G1 model may be used only to *reduce
   noise in service of the human* (the shipped `should_notify` = don't re-notify what's
   acknowledged). It may **not** drive dark patterns — acknowledgement-nudging, timing a
   prompt for compliance, or withholding information to steer a decision. (The literal
   Giskard's Dilemma; enforces Article 5 Non-Deception.)
3. **Reliability symmetry.** G2 agent-reliability scores are *visible to the agents they
   rate* (an agent can see its own score and task-class breakdown). No secret dossier on
   an actor that shapes its treatment without its knowledge.
4. **Audit every mind-model read.** Any read of a G1/G2 model by an autonomous actor
   (for routing, notification, or gating) is logged to the existing action-boundary audit
   trail as `(actor, model:g1|g2, purpose, timestamp)` — the property that keeps
   "delegation without surveillance" defensible in the other direction too.

## The one enforceable check (tracer)

Clauses 1-3 are policy; clause 4 is mechanical and is the tracer. Add to the
action-boundary aggregator (`Scripts/action-boundary-aggregate.py` → `state/governance/
action-boundary.json`) a `mind_model_reads` audit requirement, and a check (wired into
`karpathy-cherny-discipline.yml`'s existing drift gate) that any code path reading an
`operator-model`/`reliability` artifact for an autonomous decision emits an audit line.
No enforcement of clauses 1-3 in code yet — they are declared, and the tribunal reviews
them; the *read-audit* is the first mechanical bite.

## Scope, reversibility, one-way doors

- **Additive & two-way**: clauses + one audit requirement; no behavior changes until a
  consumer (routing that reads G2, notification that reads G1) is built and enforces
  them. Removing the clauses later is a contract edit, not a data migration.
- **Ship is tribunal-gated** (governance + cross-repo: the action-boundary contract is
  Demerzel-owned). This doc is the design input to that review, not a merge.
- **Firing order**: G-guard lands *before* the first G1/G2 *consumer* (routing, live
  notification) — so the guard exists the moment a mind-model actually shapes an action,
  never after.

## Non-goals

- No new persona file yet. If a `giskard` persona is later created in Demerzel, it needs
  a behavioral test in `tests/behavioral/` (persona-requirements rule) — out of scope
  here.
- No telepathy metaphor in code — G1/G2 are plain ledgers; "mind model" is shorthand for
  those two artifacts, nothing more.
