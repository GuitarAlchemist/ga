---
status: pending
priority: p2
issue_id: "013"
tags: [code-review, agent-native, mcp, chatbot]
dependencies: ["007"]
---

# P2: ProductionOrchestrator not exposed as MCP tool — agentic pipeline dark to MCP clients

## Problem Statement

The plan creates `GA.Business.Core.Orchestration` and wires `GaApi`, but does not update `GaMcpServer`. After Phase 3, the five specialized agents and full RAG pipeline are in a shared library — but no MCP tool exposes them. A Claude Code user, Cursor user, or any MCP-connected agent cannot invoke the chatbot pipeline via tool call. The plan mentions `mcp-servers/` in references but excludes it from scope.

## Findings

- `GaMcpServer` current tools: `EchoTool`, `ModeTool`, `InstrumentTool`, `AtonalTool`, `KeyTools`, `FeedReaderToolWrapper`, `WebSearchToolWrapper` — none invoke chatbot
- `mcp-servers/augment-settings-complete.json` does not register `GaMcpServer` at all (agent-native review confirmed)
- Requires todo 007 (non-streaming endpoint) to exist first — tool should call `POST /api/chatbot/chat` via HTTP, not embed `ProductionOrchestrator` directly

## Proposed Solutions

### Option A: Add GaMcpServer/Tools/ChatTool.cs + register in augment-settings-complete.json
```csharp
[McpServerToolType]
public static class ChatTool
{
    [McpServerTool]
    [Description("Ask the Guitar Alchemist chatbot a music theory or guitar question. Returns a grounded answer with chord voicings and agent routing metadata.")]
    public static async Task<string> AskChatbot(
        [Description("The music theory or guitar question")] string question,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        var client = httpClientFactory.CreateClient("gaapi");
        var response = await client.PostAsJsonAsync("/api/chatbot/chat", new { message = question });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
```
Register `GaMcpServer` in `mcp-servers/augment-settings-complete.json`.
- **Effort**: Medium (3-4h)
- **Risk**: Low

### Option B: Defer to follow-up track
Create a tracked follow-up; exclude from this plan's scope.
- **Effort**: None
- **Risk**: Low — not blocking the primary goal

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to create**: `GaMcpServer/Tools/ChatTool.cs`
- **Files to update**: `mcp-servers/augment-settings-complete.json`
- **Depends on**: Todo 007 (non-streaming REST endpoint must exist first)

## Acceptance Criteria
- [ ] MCP tool `AskChatbot` is callable from Claude Code `@gaapi` context
- [ ] Tool returns `NaturalLanguageAnswer` + `routing.agentId` in structured JSON
- [ ] `GaMcpServer` is listed in `mcp-servers/augment-settings-complete.json`

## Work Log
- 2026-03-03: Identified by agent-native-reviewer (P1-2 in their report, rated P2 here)

## Resources
- Plan: References section, Scope Boundaries
- Source: `GaMcpServer/Tools/`
