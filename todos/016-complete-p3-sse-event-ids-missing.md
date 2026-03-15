---
status: pending
priority: p3
issue_id: "016"
tags: [code-review, architecture, sse, chatbot]
dependencies: []
---

# P3: SSE events missing id: field — no reconnect capability

## Problem Statement

The plan's SSE format (`data: {...}\n\n`) has no `id:` field. The browser's native `EventSource` reconnect mechanism uses `Last-Event-ID` to resume from the last received event. Without `id:` fields, a mid-stream disconnect leaves the client with a partial accumulation and no way to resume. The `[DONE]` terminator is the only signal, so any disconnect before `[DONE]` causes a silent incomplete response.

## Proposed Solutions

### Option A: Add sequential id: to each SSE event
```
id: 1
data: {"type":"routing",...}

id: 2
data: first chunk text

id: 3
data: [DONE]
```
- **Effort**: Trivial (add counter, prepend `id: {n}\n` before each `data:` line)
- **Risk**: None — purely additive

### Option B: Defer to SignalR track
SSE reconnect semantics are complex to implement correctly (requires server-side event buffering). Accept the limitation for this milestone.

## Acceptance Criteria
- [ ] Each SSE event has a sequential `id:` line
- [ ] Browser `EventSource` `lastEventId` is populated correctly

## Work Log
- 2026-03-03: Identified by architecture-strategist (P3-B)
