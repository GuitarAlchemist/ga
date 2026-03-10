---
status: complete
priority: p1
issue_id: "008"
tags: [agent-native, mcp, chatbot, code-review]
dependencies: []
---

# 008 — `ChatTool` Posts to Non-Existent URL — `AskChatbot` Always Returns 404

## Problem Statement
`ChatTool` sends `POST /api/chatbot/chat`, but the controller only exposes `POST /api/chatbot/chat/stream`. The non-streaming route `/api/chatbot/chat` does not exist. Every call to the `AskChatbot` MCP tool returns HTTP 404, which causes `EnsureSuccessStatusCode()` to throw, making the tool completely broken. No agent using the MCP surface can query the chatbot.

## Findings
- `GaMcpServer/Tools/ChatTool.cs:17`: hardcoded URL is `"/api/chatbot/chat"`.
- `ChatbotController` (GaApi) only registers `POST /api/chatbot/chat/stream` (Server-Sent Events).
- `EnsureSuccessStatusCode()` is called on the response, so 404 propagates as an unhandled exception to the MCP caller.
- The `AskChatbot` MCP tool has been non-functional since it was introduced.

## Proposed Solutions
### Option A — Update URL and add SSE parsing
Change the URL in `ChatTool.cs` to `/api/chatbot/chat/stream`. Read the SSE response body, strip `data:` prefixes, concatenate non-`[DONE]` tokens, and return the assembled text.

**Pros:** No backend changes; reuses the existing streaming endpoint.
**Cons:** SSE parsing in a C# MCP tool is boilerplate; streaming tokens must be fully buffered before the tool can return, so the caller always waits for the full response.
**Effort:** Small
**Risk:** Low

### Option B — Add a non-streaming `POST /api/chatbot/chat` endpoint (recommended)
Add a thin controller action `POST /api/chatbot/chat` that calls `orchestrator.AnswerAsync(...)` and returns `{ "answer": "..." }` as JSON. `ChatTool.cs` deserializes the JSON response.

**Pros:** Cleaner for agent consumption — single round-trip, no SSE parsing, easy to unit-test; also useful for REST clients and integration tests.
**Cons:** Requires a new controller action and minor wiring.
**Effort:** Small
**Risk:** Low

## Recommended Action
(Leave blank — to be filled during triage)

## Technical Details
- **Affected files:**
  - `GaMcpServer/Tools/ChatTool.cs:17`
  - `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` (Option B only)
- **Components:** MCP server `ChatTool`, `ChatbotController`, chatbot orchestrator

## Acceptance Criteria
- [ ] `AskChatbot` MCP tool returns a non-empty answer string for a valid question.
- [ ] No HTTP 404 or unhandled exception is raised during a normal call.
- [ ] If Option B is chosen: `POST /api/chatbot/chat` is documented in the OpenAPI spec and covered by at least one integration test.
- [ ] If Option A is chosen: SSE parsing handles multi-chunk responses and `[DONE]` terminator correctly.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
