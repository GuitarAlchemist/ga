---
status: complete
priority: p2
issue_id: "014"
tags: [security, signalr, logging, code-review]
dependencies: []
---

# 014 — ChatbotHub: No Authorization + Raw Query Logged

## Problem Statement

`ChatbotHub` has two distinct security issues:

1. The hub class has no `[Authorize]` attribute, and `MapHub<ChatbotHub>` in `Program.cs` has no `.RequireAuthorization()` call. Any anonymous WebSocket client can connect and invoke all hub methods without authentication.

2. `logger.LogError(ex, "Error searching knowledge for query: {Query}", query)` at `ChatbotHub.cs:137` logs the raw user query verbatim into structured logs with no length truncation. A user-controlled string flowing into a structured log sink is a log injection vector and a potential PII leak.

## Findings

- No `[Authorize]` on the hub class or any hub method.
- No `.RequireAuthorization()` on the `MapHub<ChatbotHub>` endpoint registration.
- The `{Query}` structured log parameter at line 137 is the unmodified caller-supplied string with no max-length guard.

## Proposed Solutions

### Option A — Attribute on hub class + inline truncation
Add `[Authorize]` to the `ChatbotHub` class declaration. Truncate the query before logging:
```csharp
logger.LogError(ex, "Error searching knowledge for query: {Query}",
    query[..Math.Min(query.Length, 200)]);
```
**Pros:** Self-contained; survives endpoint registration refactors.
**Cons:** Requires touching the hub file; easy to miss on future hub methods.
**Effort:** Low — two-line change.
**Risk:** Low.

### Option B — RequireAuthorization at endpoint registration
In `Program.cs`, change `app.MapHub<ChatbotHub>(...)` to `app.MapHub<ChatbotHub>(...).RequireAuthorization()`. Apply the same query truncation as Option A.
**Pros:** Authorization policy lives with routing; consistent with ASP.NET Core conventions for minimal-API style registration.
**Cons:** Separate file from the hub; a developer might add a new hub and forget the call.
**Effort:** Low.
**Risk:** Low.

## Recommended Action

## Technical Details

- **Affected files:**
  - `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs` (lines 13, 137)
  - `Apps/ga-server/GaApi/Program.cs` (MapHub registration)

## Acceptance Criteria

- [ ] Unauthenticated WebSocket connections to `/chatbothub` (or equivalent path) are rejected with 401.
- [ ] The log statement at line 137 truncates the query to a bounded length before writing to structured logs.
- [ ] Existing authenticated hub integration tests remain green.
- [ ] `dotnet build AllProjects.slnx -c Debug` passes with zero warnings.

## Work Log

- 2026-03-07 — Identified in /ce:review of PR #2

## Resources

- PR: https://github.com/GuitarAlchemist/ga/pull/2
