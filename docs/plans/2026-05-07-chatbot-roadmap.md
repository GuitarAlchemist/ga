---
date: 2026-05-07
status: living
audience: engineers working on chatbot orchestration, F# DSL bridge, SignalR hub, Apps/GaChatbot.Api
supersedes_partial: docs/plans/2026-05-03-chatbot-skill-md-migration-completion.md
related:
  - docs/architecture/chat-surfaces.md
  - docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md
  - docs/plans/2026-05-03-chatbot-ix-and-ga-dsl-scoping.md
one_way_doors: [public chat wire contract, canonical-host choice]
revisit_trigger:
  - any P0 item shipped → re-rank
  - public-demo SLA changes
  - new host introduced
---

# GA chatbot roadmap

Synthesised 2026-05-07 from the multi-LLM review (codex CLI second-opinion) + live smoke set against `https://demos.guitaralchemist.com/chatbot/` + this session's eight commits.

## TL;DR

Direction is right. What's missing is enforcement (tests + observability that prove what we think is true is true) and one decision (canonical host). The fixes are small. The order is fixed by dependencies.

## Foundations (sound, leave alone)

- **Five-layer dependency model** — Core / Domain / Analysis / AI-ML / Orchestration. Stable.
- **F# closure registry → `ga_dsl_eval` bridge** — primitive is correct; verified end-to-end as of session end.
- **`SemanticIntentRouter` over keyword regex** — correctly routes 5 of 6 smoke prompts. Misroute is a tuning concern, not architectural.
- **SKILL.md-driven Path B (`SkillMdDrivenSkill` + `SkillMdDrivenWrapperBase`)** — works; today's break was registry-side, not skill-side.
- **Algebra path via `IIxAlgebraService`** — fully grounded end-to-end through deployed UI as of session end.

## Now / P0 — close the gaps the smoke set exposed

### 1. ✅ Prove Path B actually invokes `ga_dsl_eval` — *closed 2026-05-07 (`d41ee4df`)*

After the 2026-05-07 registry-bootstrap fix, transpose / common-tones / diatonic-chords returned correct answers but `grounding=none`. The 2026-05-07 follow-up wired tool-invocation tracking into `SkillMdDrivenSkill` (`response.Messages` walk for `FunctionCallContent`), made `SkillMdDrivenWrapperBase` emit a `grounding.source: ga.dsl@<closureName>` sentinel in evidence and clamp confidence ≤0.5 with a warning when `ga_dsl_eval` was *not* invoked, and taught `OrchestratorSkillIntent` to lift the sentinel into `IntentGroundingEvidence` so the closure name surfaces on the chat wire as `grounding.queryType`. Confirmed live:

```
algebra-z-relation   → grounding=ix-compatible@z-relation
transpose            → grounding=ga.dsl@domain.transposeChord
common-tones         → grounding=ga.dsl@domain.commonTones
diatonic-chords      → grounding=ga.dsl@domain.diatonicChords
```

GaApi log line `SkillMdDrivenSkill [transpose] response length=63, tool calls=ga_dsl_eval` confirms the actual function call.

### 2. Pick canonical surface (Option A / B / C / C-prime)

Three controllers nominally route the same paths with different contracts. Decide before adding more features.

- **C-prime** (codex pick) — extract `IChatApplicationService` into a host-neutral library, consume from GaApi first, freeze `GaChatbot.Api` until a concrete deploy reason emerges.
- A / B / C documented in [chat-surfaces.md §5b](../architecture/chat-surfaces.md).

**Done when:** the decision is recorded in `chat-surfaces.md`, `GaChatbot.Api` is either the canonical deployable or marked frozen, and the wire contract for `Grounding` / `Trace` is single-sourced.

### 3. ✅ Real integration test for `SkillMdDrivenSkill` — *closed 2026-05-07 (`e44f998d`)*

`Tests/Common/GA.Business.Core.Tests/AI/ChatbotOrchestrationBootstrapTests.cs` adds three NUnit tests that wire `AddChatbotOrchestration` against a real `IServiceCollection` and assert the canary closures resolve via `GaClosureRegistry.Global.TryGet` and `DslEvalMcpTools.ListClosures()`. Idempotency test covers multi-host wiring. Codex's "Done when" criterion verified: temporarily killing `GaClosureBootstrap.init()` makes all 3 tests fail; restoring returns them to passing.

## Soon / P1

### 4. ✅ Refactor 4 primitive `Grounding*` fields → `IntentGroundingEvidence` record — *closed 2026-05-07 (`367c85bf`)*

`IntentResult` now carries a single `IntentGroundingEvidence?` (record with `Source`, `Revision`, `QueryType`, `Facts`) instead of four optional primitives. `AlgebraIntent` and `OrchestratorSkillIntent` construct the record directly; `ProductionOrchestrator.BuildGrounding` is a one-line null-or-map. The next intent that needs grounding takes one parameter instead of four. Wire shape unchanged end-to-end.

### 5. Stop silent graceful degradation for deterministic skills — *partially landed in P0 #1*

P0 #1 already implements the loud-failure half: when `ga_dsl_eval` is *not* invoked by a Path B skill, `SkillMdDrivenWrapperBase` clamps confidence to ≤0.5, logs a warning, and adds a `"warning: ga_dsl_eval was NOT invoked"` evidence tag. Remaining work to fully close: surface `tool.failure_reason` as a trace tag, and define what "fail" looks like for closure-side errors (today: empty response → 0 confidence; closure-throws → swallowed in `SkillMdDrivenSkill` catch).

### 6. Improve embedding router quality (#81)

Voicing-search → modes at 0.71 confidence is a real misroute. LLM arbitration on close calls is the proposed fix. Already tracked.

### 7. One canonical chat-wire contract

After P0 #2 picks a host, port `Grounding` and `Trace` to the canonical surface so all consumers see the same shape. Today the SignalR hub strips `Trace`, the GaApi REST omits `Trace` entirely, and `GaChatbot.Api` has both — three contracts for one concept.

## Later / P2

### 8. Session-scoped `MemoryStore` (#82)

Already tracked. Re-enables retrieval safely.

### 9. Streaming truth in `AnswerStreamingAsync`

`ProductionOrchestrator.AnswerStreamingAsync` only true-streams when the selected agent is a `GuitarAlchemistAgentBase`. Tab / path / skill paths simulate streaming by word-splitting after the answer arrives. Frontends parse this as token streaming. Either fix it or own it explicitly in the AG-UI contract.

### 10. Cut or revive `GA.AI.Service`

The third controller (`GA.AI.Service.Controllers.ChatController`) resolves `ProductionOrchestrator` standalone but nobody calls it; AppHost registration commented out. Don't leave a third option dangling: delete it or wire it for a real consumer.

## What this session shipped (8 commits, 2026-05-07)

| Commit | Scope |
|---|---|
| `f4792578` | test pages — chord picker + audio strum + sunburst breadcrumb + complete Forte catalog |
| `45f5f867` | `/chatbot/` SPA-shell trailing-slash rewrite middleware |
| `5d181bd5` | slot-build fix + `docs/solutions/architecture/2026-05-07-slot-build-stale-static-web-assets-manifest.md` |
| `72fa0bc6` | `IntentResult` grounding propagation through `ProductionOrchestrator` (algebra tests 88 → 90) |
| `d36704eb` | `chat-surfaces.md §0/§5b` + GaApi `ChatJsonResponse.Grounding` |
| `fd0cbe26` | `ChatbotHub` emits `grounding` in routing payload (deployed-demo wire parity) |
| `802498e3` | `GaClosureBootstrap.init()` at startup + `ClosureRegistryStartupCheck` canary |
| `d41ee4df` | P0 #1 — Path B `ga_dsl_eval` tool-invocation tracking + grounding sentinel + confidence clamp |
| `e44f998d` | P0 #3 — `ChatbotOrchestrationBootstrapTests` regression fixture (3 tests, codex's "done when" verified) |
| `367c85bf` | P1 #4 — `IntentGroundingEvidence` record collapses the four primitive grounding fields |

## Decision log

- **2026-05-07** — chose to keep `IntentResult` in `GA.Business.ML` (layer 4) and reconstruct `GroundingMetadata` at the orchestrator boundary rather than move `IntentResult` upward. Two-way door — revisit if more intents need typed grounding.
- **2026-05-07** — canonical surface decision *deferred*; recorded options A/B/C/C-prime in `chat-surfaces.md §5b`. One-way door — re-decision is expensive once feature work resumes.
- **2026-05-07** — closure-registry bootstrap added to `AddChatbotOrchestration` rather than `DslEvalMcpTools` static constructor. Two-way door — moveable if multi-host wiring demands it.

## Out of scope here

- LLM model selection (`Anthropic:Claude` vs `Ollama` vs `Docker`) — covered by `llm-providers.md`.
- OPTIC-K voicing index correctness — owned by `docs/plans/2026-05-02-optick-sae-plan.md`.
- IXQL / Demerzel governance integration — owned by sibling repos and their own roadmaps.
