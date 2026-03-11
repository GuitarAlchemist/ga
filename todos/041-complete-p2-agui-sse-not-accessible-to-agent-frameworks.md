---
status: complete
priority: p2
issue_id: "041"
tags: [agent-native, mcp, agui, streaming, code-review]
dependencies: ["029", "035"]
---

# 041 — AG-UI SSE Stream Not Accessible to Agent Frameworks Without SSE Support

## Problem Statement

`POST /api/chatbot/agui/stream` commits `text/event-stream` headers immediately and streams AG-UI protocol events. Agent frameworks that use plain HTTP (MCP tool callers, `HttpClient.PostAsJsonAsync`, CI scripts) receive a raw SSE stream they cannot parse. The existing `POST /api/chatbot/chat` JSON endpoint works for agents but does not emit the typed AG-UI events (`ga:diatonic`, `ga:candidates`, `ga:progression`), so agents cannot consume the structured domain data that the new stream provides.

This creates a capability gap: domain-structured data (key candidates, diatonic sets) is only available through a streaming protocol that non-browser agents cannot consume.

## Findings

`Apps/ga-server/GaApi/Controllers/AgUiChatController.cs` line 49: `Response.StartAsync()` commits SSE headers before any content is written — non-streaming clients receive garbage.

`GaMcpServer/Tools/ChatTool.cs` — `AskChatbot` MCP tool calls `POST /api/chatbot/chat` (the old JSON endpoint), not the AG-UI stream, so it misses typed domain events.

## Proposed Solutions

### Option A — Add `POST /api/chatbot/agui/json` non-streaming sibling (Recommended)
A sibling endpoint that runs the same `ProductionOrchestrator.AnswerAsync` path and returns the final AG-UI event sequence as a JSON array (not a stream):
```csharp
[HttpPost("/api/chatbot/agui/json")]
public async Task<IActionResult> AgUiJson([FromBody] RunAgentInput input, ...)
{
    var events = new List<object>();
    // collect events into list instead of writing to SSE stream
    return Ok(events);
}
```
- **Effort:** Small — shares orchestrator; only the event sink changes.
- **Risk:** Low.

### Option B — Enhance `AskChatbot` MCP tool to call `agui/json`
Once Option A exists, update `ChatTool.AskChatbot` (or add `AskChatbotStructured`) to call the JSON endpoint and return typed domain data.
- **Effort:** Small.

### Option C — Document SSE as the sole interface; accept the gap
- **Risk:** Agents cannot access structured domain events.

## Recommended Action
Option A + B.

## Acceptance Criteria

- [ ] `POST /api/chatbot/agui/json` returns all AG-UI events as a JSON array
- [ ] MCP tool can call the JSON endpoint and return typed domain data (key candidates, diatonic sets)
- [ ] Claude Code can request structured progression analysis without parsing SSE

## Work Log

- 2026-03-10: Identified during agent-native review agent for PR #8
