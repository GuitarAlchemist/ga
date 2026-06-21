---
title: "A cross-repo tool's description lied about what it does — validate the keystone from source before building toward it"
date: 2026-06-20
module: "GA.Business.ML (chatbot) ↔ ../tars (knowledge graph)"
component: "TARS graph_find_contradictions / temporal_detect_contradictions; GA→TARS claim contract + ADR-0003"
problem_type: learning
decision: "Validate a dependency's ACTUAL semantics from its source before designing a contract or writing producer code against it — and when a build has a known-easy half and an unknown keystone, attack the keystone first."
rejected:
  - "Build the GA-side producer first (the easy, known half) — tactical motion that leaves the existential risk unexamined"
  - "Trust the MCP tool catalog description ('Detect contradictory assertions in the knowledge graph')"
reason: "The tool *description* implied content-based detection; the *implementation* only returns relationships already typed `ContradictedBy`/`Contradicts`. Reading 10 lines of F# caught a design-invalidating keystone error before a single line of producer code — and before the (already-merged) ADR/contract misled future agents further."
date_decided: 2026-06-20
tags: [cross-repo, tars, knowledge-graph, contradiction-detection, keystone-validation, strategic-vs-tactical, contract-design, mcp, validate-before-build, descriptions-lie]
---

# TARS *stores* contradictions, it doesn't *detect* them — validate the keystone first

## Symptom

A `/grill-with-docs` session designed (and merged) a plan to wire TARS as GA's "self-consistency oracle":
ingest the chatbot's typed claims and let TARS's `graph_find_contradictions` /
`temporal_detect_contradictions` flag when the chatbot asserted *X* in one session and *¬X* in another.
ADR-0003 + a GA→TARS claim contract shipped on this premise. The next step looked like "build the GA-side
claim extractor (producer)."

## The catch (what almost happened)

The plan had a **known-easy half** (GA extracts a claim from prose — a solvable NLP problem) and an
**unknown keystone** (does TARS actually detect the contradiction we hand it?). The tempting move was to
build the easy half first — it *feels* productive. That would have produced a fully-tested producer
feeding a consumer that **cannot do the one job the design depends on.**

Instead, the keystone was validated first, from TARS's own source:

```fsharp
// ../tars/v2/src/Tars.Core/BeliefGraph.fs
let findContradictions (graph: BeliefGraph) =
    graph.Edges
    |> List.filter (fun e -> match e.Relation with ContradictedBy _ -> true | _ -> false)
    |> List.choose (...)            // returns belief pairs ALREADY linked by a ContradictedBy edge

// ../tars/v2/src/Tars.Core/TemporalReasoning.fs
let detectContradictions (graph) (atTime) =
    graph.GetSnapshot(atTime)
    |> List.choose (function Contradicts(s, t, reason) -> Some(s, t, reason) | _ -> None)
```

Both "detect" functions **filter for relationships already asserted as `ContradictedBy` / `Contradicts`
edges.** Neither compares assertion *content*. **TARS is a contradiction *ledger*, not a *detector*.**

## Root cause (of the design error)

The MCP tool catalog described `graph_find_contradictions` as *"Detect contradictory assertions in the
knowledge graph"* — and the signature (`{ threshold: 0.5 }`, beliefs with `confidence`) *implied*
semantic/probabilistic detection. The design trusted that description. The implementation does no such
thing: detection must be done **upstream** and the contradiction asserted into the graph as an edge.

## Solution

- **GA does the detection** — same-`key`-different-`asserted_value` is a trivial dictionary compare — and
  emits to `state/quality/consistency/`. **TARS is demoted to an optional downstream ledger** (assert the
  already-detected contradiction as an edge for cross-repo/temporal persistence). The renamed backlog item
  is `ga-claim-consistency-checker`; it has **no TARS dependency**.
- The merged ADR-0003 + contract were **corrected the same day** (PR #454) with a § Correction and a
  keystone banner, so no future agent builds against the false premise.
- (Aside, separately tracked in `../tars/`: TARS's MCP output layer is broken — tools return un-awaited
  `Task` objects, e.g. `health_check` → `System.Threading.Tasks.Task\`1[System.Boolean]`.)

## Prevention (the compounding lessons)

1. **Validate the keystone before building toward it.** When a plan has a known-easy half and an
   unknown-load-bearing half, the easy half is a trap — build it and you've invested in the part that was
   never at risk. Spend the first hour proving the thing most likely to invalidate the whole design.
2. **A tool's description is a claim, not a contract — read the implementation.** "Detect contradictions"
   meant "query pre-asserted contradiction edges." For cross-repo integrations, the *source* is the spec;
   catalogs, descriptions, and even signatures mislead. Ten lines of reading beat an hour of rework.
3. **Signature smells are real but ambiguous.** `threshold` + `confidence` + "beliefs" *looked* like
   semantic detection — and was actually an edge filter. A suggestive signature is a reason to read the
   code, not to assume either way.
4. **Strategic over tactical, concretely.** "Your call strategically, not tactically" resolved to: don't
   optimize the known/easy; resolve the dominant unknown. The payoff was a design-invalidating fact caught
   for the cost of reading two functions.

## Related

- `docs/adr/0003-tars-validates-consistency-not-truth.md` (§ Correction) — the corrected decision
- `docs/contracts/2026-06-20-ga-tars-claim.contract.md` — keystone-correction banner
- `docs/solutions/architecture/2026-06-20-voicing-search-strategy-filter-parity.md` — sibling lesson ("confirm what actually runs/matters before building toward it"); same session
- PRs #451 (original design), #454 (correction)
