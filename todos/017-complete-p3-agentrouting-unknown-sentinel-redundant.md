---
status: pending
priority: p3
issue_id: "017"
tags: [code-review, simplicity, chatbot]
dependencies: []
---

# P3: AgentRoutingMetadata.Unknown sentinel is redundant with nullable

## Problem Statement

The plan defines `AgentRoutingMetadata.Unknown` as a sentinel for "routing did not run." `ChatResponse.Routing` is already `AgentRoutingMetadata?` — `null` already expresses this. The `Unknown` sentinel adds a special-case value that every caller must check alongside `null`.

## Proposed Solutions

### Option A: Remove Unknown sentinel, use null (Recommended)
Do not write `AgentRoutingMetadata.Unknown`. The SSE routing event is emitted only when `Routing != null`. Controllers/tests check `response.Routing is null` instead of `response.Routing == AgentRoutingMetadata.Unknown`.
- **Effort**: None (do not write it)
- **Risk**: None

## Acceptance Criteria
- [ ] `AgentRoutingMetadata` has no `Unknown` static property
- [ ] `ChatResponse.Routing` is `AgentRoutingMetadata?` — null means routing did not run

## Work Log
- 2026-03-03: Identified by code-simplicity-reviewer
