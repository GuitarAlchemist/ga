---
id: 2026-08-02-agentic-architecture-catalog-evaluation
date: 2026-08-02
status: active
domain: code
question: Which terms and mechanisms from all-agentic-architectures, graph-engineering, and GitHub's agent-architecture topic should GA adopt without introducing another agent runtime or weakening Cloud Factory authority boundaries?
hypotheses:
  - claim: GA should adopt a small pattern-selection vocabulary and benchmark discipline, but not the Python/LangGraph runtime or any model-issued approval/fail-accepted semantics.
    refuted_if: The sources contain a unique transactional coordination, evidence, or authorization mechanism that GA lacks and cannot consume independently, or the package is a better canonical runtime fit than GA's .NET/Demerzel/Agent Blackbox stack.
tools: [official-github-source, official-repository-metadata, source-article-analysis, local-code-and-contract-inspection]
artifacts: null
validators: [primary-source-and-local-contract-analysis]
confidence: medium-high
supersedes: null
superseded_by: null
---

# Agentic architecture catalogs for GA: vocabulary and fit, not a new runtime

**Status caveat:** the source/code comparison is complete, but the study remains
`active` until the terminology and any architecture-fit harness receive an
independent review on their exact SHA.

## TL;DR

Use [`all-agentic-architectures`](https://github.com/FareedKhan-dev/all-agentic-architectures)
as a compact pattern textbook and negative-example catalog. Adopt its architecture
families, architecture-fit benchmark concept, and categorical-feature composition
idea. Do not install its Python/LangGraph runtime into GA or make a dynamic
meta-controller part of the Cloud Factory tracer bullet.

GA must strengthen two reference patterns:

- Dry-Run becomes **preflight simulation** grounded in read-only facts plus
  deterministic policy/operator approval; a model may not predict risk and then
  authorize the action.
- PEV becomes fail-closed **Plan–Execute–Verify**; retry exhaustion yields a
  blocked packet, never the upstream `fail-accepted` continuation.

GitHub's [`agent-architecture` topic](https://github.com/topics/agent-architecture)
is useful only as a changing discovery feed. Repository tags, descriptions, and
stars are not evidence. Candidate mechanisms must be traced to maintained source,
tests, license, primary references, and a GA-specific benchmark.

[`codejunkie99/graph-engineering`](https://github.com/codejunkie99/graph-engineering)
and Louis Bouchard's [explanation](https://www.louisbouchard.ai/graph-engineering-explained/)
sharpen a separate point: graphs organize bounded loops; they do not replace the
loop or make multi-agent work inherently better. Adopt real-edge, diamond,
external-evidence, and stop-rule discipline. Do not add a graph when the work is
sequential, and never let agreement inside the graph stand in for a real oracle.

## Method

Primary repository inspection:

```powershell
gh api 'repos/FareedKhan-dev/all-agentic-architectures/git/trees/main?recursive=1' --jq '.tree[].path'
gh api repos/FareedKhan-dev/all-agentic-architectures/contents/docs/tutorials/deterministic-picker.md --jq .download_url
gh api repos/FareedKhan-dev/all-agentic-architectures/contents/src/agentic_architectures/architectures/dry_run.py --jq .download_url
gh api repos/FareedKhan-dev/all-agentic-architectures/contents/src/agentic_architectures/architectures/pev.py --jq .download_url
```

Current topic snapshot (discovery only):

```powershell
gh search repos 'topic:agent-architecture' --limit 40 --sort stars --order desc `
  --json fullName,description,stargazersCount,pushedAt,url,language,license
```

Local comparison targets: the Cloud Factory reconciliation plan, Agent Blackbox
task/evidence contracts, Demerzel Gaia claims, GA's supervised-loop controls, and
the Hermes/OpenClaw mechanism study.

## Evidence

The repository currently presents a uniform `.run(task)` Python library over 35
patterns and groups them into reasoning/reflection, sampling/search, retrieval,
memory, tools/actions, multi-agent, safety/routing, and specialty families. It
uses LangGraph and an MIT license. Its own benchmark is small—17 tasks and 42
applicable architecture/task attempts—and shows meaningful pattern-fit failures:
Debate and Ensemble fail the trick-logic task, LATS fails its arithmetic task,
and several memory patterns fail stateful recall
([README](https://github.com/FareedKhan-dev/all-agentic-architectures),
[benchmark](https://github.com/FareedKhan-dev/all-agentic-architectures/blob/main/docs/benchmarks.md)).

That is useful evidence **against** a universal architecture. GA should record a
packet's task class and choose the cheapest already-approved workflow that clears
its own representative evals.

The upstream deterministic-picker tutorial asks the LLM for typed categorical
features and lets Python compose the deciding signal. This is more auditable than
asking a model for one flat numeric score, but the name can overstate the result:
composition is deterministic; the proposed features are not. GA therefore calls
the mechanism **deterministic feature composition** and excludes it from direct
Git/lease/authority facts
([tutorial](https://github.com/FareedKhan-dev/all-agentic-architectures/blob/main/docs/tutorials/deterministic-picker.md)).

The Dry-Run implementation has a Python hard cap but obtains irreversibility from
the LLM and delegates lower-severity approval to an LLM reviewer. The execution
itself is mocked. This is an educational illustration, not an authorization
boundary. GA should retain exact target resolution, protected-path/command policy,
and explicit human authority for consequential actions
([source](https://github.com/FareedKhan-dev/all-agentic-architectures/blob/main/src/agentic_architectures/architectures/dry_run.py)).

The PEV implementation verifies each step and retries with critique, but after
retry exhaustion it records `fail-accepted` and continues. That availability
trade-off contradicts the Cloud Factory contract. GA must persist the failed
evidence and transition the packet to `blocked`/`quarantined`
([source](https://github.com/FareedKhan-dev/all-agentic-architectures/blob/main/src/agentic_architectures/architectures/pev.py)).

The topic search is heterogeneous: textbooks, frameworks, samples, product
standards, internal analyses, and repositories with missing/varied licenses all
share the same tag. It is therefore suitable for periodic low-cost discovery,
not automatic installation or nomenclature changes.

## Graph-engineering cross-check

The graph-engineering repository distinguishes two different products. A
**knowledge graph** is a governed data pipeline—scope, representation, ontology,
entity/relation/event extraction, quality gate, fusion, and a supported query
surface. A **task graph** arranges bounded jobs/agent loops around actual data or
execution dependencies. GA should preserve that distinction: Graphiti or another
retrieval graph is narrative/knowledge infrastructure, while Gaia's work graph is
coordination. Neither is the transactional authority ledger.

The most useful task shape is a bounded diamond: split genuinely decomposable
work; give parallel workers separate contexts; verify their outputs independently;
then converge through one deterministic combiner or explicitly authorized owner.
Edges that merely serialize work or move a giant shared prompt are fake edges and
should be removed. “Owned merge” means ownership of the convergence function or
decision; it does not grant an agent Git merge authority.

Bouchard's explanation correctly frames graph engineering as older workflow/DAG/
state-machine structure applied to probabilistic nodes. Because those nodes can
confidently reinforce each other's errors, extra reviewers can create organized
nonsense. The countermeasure is not agent voting: it is inspectable state, a hard
budget/stop, a fresh verifier context, and evidence outside the graph—tests,
compiler/runtime facts, protected-system state, customer/economic outcomes, or an
accountable human gate. GA strengthens the recommendation by binding technical
evidence to an exact SHA and packet generation.

The practical adoption rule is therefore cost-sensitive: start with one loop for
one recurring task and one real verifier. Add a graph only when a measured task
has real dependencies or parallel branches that materially benefit from it. Every
consequential route needs a veto edge, and every loop needs a deterministic stop
rule. This aligns with GA's tracer-bullet/vertical-slice discipline and its rule
that heavy multi-agent fan-out requires an explicit cost estimate and approval.

## Verdict and Cloud Factory delta

1. Add a canonical agentic-engineering glossary at
   `docs/agents/nomenclature.md`; use contract schemas as the narrower authority.
2. Record `architecture_family`, `architecture_name`, and version/digest in a
   packet's capability/trace metadata, not its authority grant.
3. Start with an explicit typed mapping from packet class to approved workflow.
   Add a meta-controller only if a held-out architecture-fit benchmark shows the
   mapping is insufficient.
4. If a model contributes to selection, accept only typed categorical features;
   validate them and compose the route in code. Treat the route as a proposal
   whenever it would cross an authority boundary.
5. Keep preflight simulation and postflight verification distinct and
   fail-closed. Never copy `fail-accepted` into Gaia, Agent Blackbox, CI verdicts,
   or merge readiness.
6. Review the GitHub topic at major program gates or when a concrete mechanism is
   missing—not as a scheduled architecture churn loop.
7. Represent only real task/data dependencies as graph edges. Keep sequential
   work in one bounded loop; use diamonds only for independently verifiable
   parallel branches with one owned convergence step.
8. Require external evidence and a fail-closed veto on every consequential graph
   path. Reviewer consensus, shared narrative memory, and task-graph topology do
   not create authority.

No new runtime dependency is justified. The source improves the Cloud Factory's
language and future evaluation matrix while leaving its tracer-bullet architecture
and authority boundaries intact.
