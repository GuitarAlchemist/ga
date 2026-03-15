---
status: pending
priority: p1
issue_id: "007"
tags: [code-review, agent-native, api, chatbot]
dependencies: []
---

# P1: No non-streaming POST /api/chatbot/chat endpoint — pipeline inaccessible to programmatic consumers

## Problem Statement

The plan's scope boundaries explicitly exclude a non-streaming fallback endpoint. However, `ProductionOrchestrator.AnswerAsync` already returns a complete `ChatResponse` — the streaming is simulated server-side. Excluding the non-streaming endpoint makes the entire agentic pipeline (routing metadata, RAG candidates, agent selection) inaccessible to: MCP tool invocations, integration test harnesses, CI pipelines, other services calling the API, and any HTTP client that does not support SSE.

## Findings

- Plan Scope Boundaries: "Not in scope: Non-streaming `POST /api/chatbot/chat` fallback endpoint"
- `ProductionOrchestrator.AnswerAsync` returns `Task<ChatResponse>` (complete, not streamed) — confirmed by plan's own note in Phase 4
- A non-streaming endpoint requires zero new logic: `return Ok(await _orchestrator.AnswerAsync(request))`
- Agent-native-reviewer: "3/9 capabilities are agent-accessible" with current plan; non-streaming endpoint raises this to "8/9"
- `ChatResponse` already has all fields: `NaturalLanguageAnswer`, `Candidates`, `Progression`, `Routing` (new)
- `GaMcpServer` MCP tool needs this endpoint to exist (see todo 008)

## Proposed Solutions

### Option A: Add POST /api/chatbot/chat to Phase 4 (Recommended)
```csharp
// ChatbotController.cs:
[HttpPost("chat")]
public async Task<ActionResult<ChatResponse>> Chat(
    [FromBody] ChatRequest request,
    CancellationToken cancellationToken)
{
    // identical validation as ChatStream
    var response = await _orchestrator.AnswerAsync(request, cancellationToken);
    return Ok(response);
}
```
Verify `ChatResponse` and all nested types (`AgentRoutingMetadata`, `CandidateVoicing`, `Progression`) are JSON-serializable (no circular refs, no `[JsonIgnore]` on needed fields).
- **Pros**: Zero new logic; immediately enables all programmatic consumers
- **Cons**: None — the data is already computed
- **Effort**: Trivial (30 minutes including tests)
- **Risk**: Low

### Option B: Keep scope boundary, create follow-up issue
Accept the agent-native gap and create a tracked follow-up.
- **Pros**: Preserves milestone scope
- **Cons**: MCP tool (todo 008) cannot be implemented without this; programmatic consumers blocked
- **Effort**: None
- **Risk**: Medium — blocks other work

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to update**: `Apps/ga-server/GaApi/Controllers/ChatbotController.cs`
- **Files to verify serializable**: `Common/GA.Business.Core.Orchestration/Models/ChatModels.cs`, `AgentRoutingMetadata.cs`
- **Phase in plan**: Phase 4 (add alongside existing ChatStream action)

## Acceptance Criteria
- [ ] `POST /api/chatbot/chat` returns `200 OK` with `ChatResponse` JSON body
- [ ] Response includes `routing.agentId`, `routing.confidence`, `routing.routingMethod`
- [ ] Response includes `candidates` list
- [ ] Integration test: `ChatNonStreamingTests.cs` verifies all fields
- [ ] `curl -X POST /api/chatbot/chat -d '{"message":"what is Cmaj7?"}' -H 'Content-Type: application/json'` works without SSE client

## Work Log
- 2026-03-03: Identified by agent-native-reviewer (P1-1, highest priority gap)

## Resources
- Plan: `docs/plans/2026-03-02-refactor-chatbot-orchestration-extraction-plan.md` Phase 4, Scope Boundaries
- Source: `Apps/ga-server/GaApi/Controllers/ChatbotController.cs`
