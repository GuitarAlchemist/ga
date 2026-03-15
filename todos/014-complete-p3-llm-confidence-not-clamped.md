---
status: pending
priority: p3
issue_id: "014"
tags: [code-review, security, chatbot, validation]
dependencies: []
---

# P3: LLM-supplied confidence not clamped to [0.0, 1.0] — out-of-range values stream to frontend

## Problem Statement

`SemanticRouter.LlmRouteAsync` deserializes `confidence` from LLM JSON output with no range validation. The LLM can return `99.0` or `-0.5`. These values propagate to `AgentRoutingMetadata.Confidence` and are rendered in the React badge as `9900%` or `-50%`.

## Proposed Solutions

### Option A: Clamp in AgentRoutingMetadata record constructor
```csharp
public sealed record AgentRoutingMetadata(string AgentId, double Confidence, string RoutingMethod)
{
    public double Confidence { get; init; } = Math.Clamp(Confidence, 0.0, 1.0);
}
```
Also guard in React: `Math.round(Math.min(Math.max(confidence, 0), 1) * 100)`.
- **Effort**: Trivial
- **Risk**: None

## Acceptance Criteria
- [ ] `AgentRoutingMetadata` with `Confidence = 99.0` renders as 100% in UI
- [ ] `AgentRoutingMetadata` with `Confidence = -0.5` renders as 0%
- [ ] React badge uses `Math.min/max` clamp

## Work Log
- 2026-03-03: Identified by security-sentinel (P3)
