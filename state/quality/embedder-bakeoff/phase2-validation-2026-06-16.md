# Embedder bake-off — Phase 2 validation (bge-large)

The Phase 0 winner (`bge-large`) run through the **full** `RoutingEvalHarness`
(descriptions + hint boosts + threshold — the real production stack, not the
cosine-1NN proxy), then threshold-recalibrated. Verdict: **promotable — beats
nomic on both in-scope accuracy and OOS-decline, conditional on recalibrating
`MinConfidence`.**

## Full-harness result vs nomic baseline

| embedder | threshold | in-scope acc | OOS-decline |
|---|---|---:|---:|
| nomic-embed-text (production) | 0.55 | 0.934 | 0.875 |
| bge-large | 0.55 (nomic's) | **0.946** | 0.375 |
| **bge-large** | **0.64 (recalibrated)** | **0.946** | **1.000** |

- **in-scope 0.946 (+1.2pt over nomic)** — bge-large genuinely improves routing
  through the production stack, confirming the Phase 0 proxy (+6.1pt CV) and the
  +37% silhouette.
- At nomic's 0.55 threshold, **OOS-decline collapses to 0.375** — NOT a bge-large
  flaw: bge-large's cosine scores run higher, so out-of-scope queries that nomic
  scored below 0.55 (→ declined) now clear 0.55 (→ force-routed). A
  **calibration artifact**.

## Threshold recalibration (offline, from the emitted per-prompt report)

| T | in-scope | OOS-decline |
|---:|---:|---:|
| 0.55 | 0.946 | 0.375 |
| 0.62 | 0.946 | 0.750 |
| **0.64** | **0.946** | **1.000** |
| 0.66 | 0.946 | 1.000 |
| 0.70 | 0.940 | 1.000 |
| 0.72 | 0.934 | 1.000 |
| 0.75 | 0.916 | 1.000 |

At **T = 0.64–0.68**, bge-large beats nomic on **both** dimensions simultaneously:
**+1.2pt in-scope AND +12.5pt OOS-decline**. In-scope holds flat to ~0.70 then
declines as the threshold starts rejecting correct routes.

## Conclusion & feedback into the plan

1. **bge-large clears the Phase 2 gate** (beats the 0.934 production in-scope) and
   improves OOS-decline — promote it (after Phase 1 decouples the routing embedder
   so the swap doesn't touch the memory store).
2. **The threshold is embedder-specific.** `SemanticIntentRouter.DefaultMinConfidence`
   (0.55, tuned for nomic) must be **recalibrated to ~0.64–0.66 for bge-large** as
   part of the swap. Phase 2 of the plan must include this — a per-embedder
   constant, not a global one. The `RoutingEvalHarness.RouterThreshold_MatchesProductionDefault`
   guard already pins harness↔prod threshold parity, so set both together.
3. The recalibration sweep should be re-run from the emitted per-prompt report on
   any future embedder swap (the cosine scale shifts each time).

Caveat: the labeled corpus is small (166 in-scope / 8 OOS), so the OOS-decline
numbers (n=8) are coarse — directionally clear, but confirm on a larger OOS set
as the corpus / shadow data grows.
