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

### 1. Prove Path B actually invokes `ga_dsl_eval` (don't just trust the answer)

After the 2026-05-07 registry-bootstrap fix, transpose / common-tones / diatonic-chords return correct answers, but `grounding=none`. We don't know whether the LLM is calling the closure or just inspecting the closure list and computing the answer from its training data.

- Wire `Evidence: ["ga_dsl_eval(domain.transposeChord, …)"]` into `IntentResult.Evidence` by recording each `[McpServerTool]` invocation per request.
- If `ga_dsl_eval` was *not* called, drop confidence to 0 and fail loud in the trace — don't degrade silently.
- Add `IntentGroundingEvidence.Source = "ga.dsl@<closure>"` for Path B intents (see P1 #4 for the typed shape).

**Done when:** the four Path B prompts in the smoke set surface a non-null `grounding` block on the wire, sourced from the actual tool call.

### 2. Pick canonical surface (Option A / B / C / C-prime)

Three controllers nominally route the same paths with different contracts. Decide before adding more features.

- **C-prime** (codex pick) — extract `IChatApplicationService` into a host-neutral library, consume from GaApi first, freeze `GaChatbot.Api` until a concrete deploy reason emerges.
- A / B / C documented in [chat-surfaces.md §5b](../architecture/chat-surfaces.md).

**Done when:** the decision is recorded in `chat-surfaces.md`, `GaChatbot.Api` is either the canonical deployable or marked frozen, and the wire contract for `Grounding` / `Trace` is single-sourced.

### 3. Real integration test for `SkillMdDrivenSkill`

`TransposeSkillTests` fakes `IChatClient` and `IMcpToolsProvider` — that's structurally why nobody noticed `GaClosureBootstrap.init()` was never called. The same shape of test will not catch the next regression of this kind.

- One NUnit fixture wiring the actual DI container + real `GaClosureRegistry.Global` (post-bootstrap) and asserting `ga_dsl_eval(domain.transposeChord, {...})` returns a known result string.
- Cover the same closures the `ClosureRegistryStartupCheck` canaries: transpose / commonTones / diatonicChords.

**Done when:** killing `GaClosureBootstrap.init()` from `AddChatbotOrchestration` makes the new fixture fail, not just `ClosureRegistryStartupCheck` log a warning.

## Soon / P1

### 4. Refactor 4 primitive `Grounding*` fields → `IntentGroundingEvidence` record

Codex flagged the four-field shape on `IntentResult` as a regression-fix smell. Move to a neutral ML-owned record before another intent adds the same fields.

```csharp
// Common/GA.Business.ML/Agents/Intents/IIntent.cs
public sealed record IntentGroundingEvidence(
    string Source,                                     // "ix" | "ix-compatible" | "ga.dsl@<closure>"
    string Revision,
    string? QueryType = null,
    IReadOnlyDictionary<string, string>? Facts = null);

public sealed record IntentResult(
    string Answer,
    float Confidence = 1.0f,
    IReadOnlyList<string>? Evidence = null,
    string? RoutingMethodOverride = null,
    IntentGroundingEvidence? Grounding = null);
```

`AlgebraIntent` constructs the record; `ProductionOrchestrator.BuildGrounding` becomes a one-line mapper.

### 5. Stop silent graceful degradation for deterministic skills

If `ga_dsl_eval` fails or wasn't invoked when a Path B intent was selected:
- `IntentResult.Confidence = 0`
- `Evidence` includes `"ga_dsl_eval: failed | not-invoked"`
- Trace tags include `tool.failure_reason`
- The user-facing answer can still come from the LLM, but it should not pretend to be deterministic.

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

## Decision log

- **2026-05-07** — chose to keep `IntentResult` in `GA.Business.ML` (layer 4) and reconstruct `GroundingMetadata` at the orchestrator boundary rather than move `IntentResult` upward. Two-way door — revisit if more intents need typed grounding.
- **2026-05-07** — canonical surface decision *deferred*; recorded options A/B/C/C-prime in `chat-surfaces.md §5b`. One-way door — re-decision is expensive once feature work resumes.
- **2026-05-07** — closure-registry bootstrap added to `AddChatbotOrchestration` rather than `DslEvalMcpTools` static constructor. Two-way door — moveable if multi-host wiring demands it.

## Out of scope here

- LLM model selection (`Anthropic:Claude` vs `Ollama` vs `Docker`) — covered by `llm-providers.md`.
- OPTIC-K voicing index correctness — owned by `docs/plans/2026-05-02-optick-sae-plan.md`.
- IXQL / Demerzel governance integration — owned by sibling repos and their own roadmaps.
