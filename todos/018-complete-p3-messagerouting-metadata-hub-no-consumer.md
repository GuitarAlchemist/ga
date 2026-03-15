---
status: pending
priority: p3
issue_id: "018"
tags: [code-review, simplicity, signalr, chatbot]
dependencies: []
---

# P3: MessageRoutingMetadata hub event has no frontend consumer — defer

## Problem Statement

Phase 4 Task 4 adds a new `MessageRoutingMetadata` SignalR client event to `ChatbotHub`. The React frontend uses SSE via `ChatbotController` — it does not connect to the hub for chat at all (`chatService.ts` uses `fetch`, confirmed by simplicity reviewer). The new hub event has no consumer.

## Proposed Solutions

### Option A: Defer MessageRoutingMetadata event, update hub to ProductionOrchestrator only
Update `ChatbotHub` to inject `ProductionOrchestrator` (necessary to prevent divergence), but do not add `MessageRoutingMetadata` event until a SignalR chat client exists.
- **Effort**: Reduction in scope (remove task 4b from Phase 4)
- **Risk**: None

### Option B: Keep as planned
Implement even with no consumer. Documents the contract for future SignalR clients.
- **Effort**: Small
- **Risk**: Dead code

## Acceptance Criteria
- [ ] Decision documented: MessageRoutingMetadata either deferred or implemented with a follow-up issue
- [ ] Hub updated to use ProductionOrchestrator (required regardless)

## Work Log
- 2026-03-03: Identified by code-simplicity-reviewer (P2 in their report)
