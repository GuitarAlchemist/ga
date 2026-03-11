---
status: complete
priority: p3
issue_id: "005"
tags: [code-review, chatbot, memory, reliability]
dependencies: []
---

# ChatbotHub _conversations dictionary has no per-session message size cap

## Problem Statement

`ChatbotHub._conversations` is a `ConcurrentDictionary<string, List<ChatMessage>>` that grows indefinitely per connection. While entries are removed on disconnect, a long-running session accumulates all messages in memory. There is also no guard against abnormal disconnects leaving stale entries (e.g., network drops where `OnDisconnectedAsync` may not be called promptly).

## Findings

- `ChatbotHub.cs:20`: `private static readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new();`
- `SendMessage` line 63: history is written without any size limit
- `sessionOrchestrator.NormalizeHistory` is called (which caps history for the LLM context), but the raw `_conversations` list is uncapped
- If Ollama takes >25s, the `OperationCanceledException` path does NOT write the assistant reply to history — consistent, but leaves the user message written with no response

## Proposed Solutions

### Option A: Cap the stored history list (Recommended)

```csharp
// After NormalizeHistory, trim to last N messages before storing
const int MaxStoredMessages = 50;
var trimmed = updatedHistory.TakeLast(MaxStoredMessages).ToList();
_conversations[connectionId] = trimmed;
```

**Pros:** Bounds memory per connection; trivial change
**Cons:** None
**Effort:** Small
**Risk:** None

### Option B: Use an in-memory cache with sliding expiration (IMemoryCache)

Replace the dictionary with `IMemoryCache` with a 30-minute sliding expiration per connectionId.

**Pros:** Handles stale disconnect cases automatically
**Cons:** More complexity; adds DI dependency
**Effort:** Medium
**Risk:** Low

## Technical Details

- **Affected files:** `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs`

## Acceptance Criteria

- [ ] History stored per connection is bounded (e.g., last 50 messages)
- [ ] No functional regression on normal conversations

## Work Log

- 2026-03-06: Found during code review of PR #2
