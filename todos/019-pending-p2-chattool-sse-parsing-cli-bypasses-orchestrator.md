---
status: pending
priority: p2
issue_id: "019"
tags: [agent-native, mcp, chatbot, code-review]
dependencies: []
---

# 019 — ChatTool SSE Wire Format Leaked to Agents + CLI Bypasses Orchestrator

## Problem Statement

Two related issues cause agents and CLI users to receive inconsistent or malformed chatbot responses:

**Issue 1 — ChatTool SSE parsing:** Even after fixing the ChatTool URL (see todo 008), the SSE response emits a `routing` JSON event first, then sentence chunks, then `[DONE]`. The current `ChatTool` reads the raw response body and returns it verbatim. MCP agents therefore receive raw SSE wire format (`data: {...}\ndata: chunk\ndata: [DONE]`) instead of a clean answer string.

**Issue 2 — CLI bypasses orchestrator:** `ga ask` in `GaCli/Program.fs:296` invokes `agent.theoryAgent` directly, bypassing `SemanticRouter`, `TabAgent`, `ComposerAgent`, `CriticAgent`, and `SpectralRagOrchestrator`. The CLI and the MCP tool give different answers to the same question because they route through different pipelines.

## Findings

- `GaMcpServer/Tools/ChatTool.cs`: raw `HttpResponseMessage` body returned without SSE parsing.
- `Apps/GaCli/Program.fs:296`: `cmdAsk` calls `agent.theoryAgent` closure, not a full-orchestrator closure.
- Both issues stem from the absence of a simple non-streaming JSON endpoint that wraps the full orchestration pipeline.

## Proposed Solutions

### Option A — Parse SSE in ChatTool
In `ChatTool.cs`, read the response stream line-by-line, strip `data:` prefixes, skip `[DONE]` and the `routing` event, and concatenate text portions:
```csharp
var lines = await response.Content.ReadAsStringAsync(ct);
var answer = string.Concat(
    lines.Split('\n')
         .Where(l => l.StartsWith("data:") && !l.Contains("[DONE]"))
         .Select(l => JsonDocument.Parse(l["data:".Length..].Trim())
                                  .RootElement
                                  .TryGetProperty("text", out var t) ? t.GetString() : null)
         .Where(s => s is not null));
```
**Pros:** Fixes agents immediately without backend changes.
**Cons:** Fragile — any SSE envelope change breaks this parser; does not fix CLI.
**Effort:** Low-Medium.
**Risk:** Medium (tight coupling to SSE format).

### Option B — Update CLI to use full-orchestrator closure
In `GaCli/Program.fs:296`, replace `agent.theoryAgent` with `agent.orchestrate` (or equivalent full-pipeline closure) so CLI uses the same routing as the MCP tool.
**Pros:** Consistent answers across CLI and MCP; minimal change.
**Cons:** Requires `agent.orchestrate` closure to exist and be registered; does not fix SSE parsing in ChatTool.
**Effort:** Low (if closure exists), Medium (if it must be added).
**Risk:** Low.

### Option C — Add a non-streaming /api/chatbot/chat JSON endpoint (preferred)
Add a new `POST /api/chatbot/chat` endpoint that calls the full orchestration pipeline and returns `{ "answer": "..." }` JSON. Update `ChatTool.cs` to call this endpoint. Update `cmdAsk` in `GaCli/Program.fs` to call this endpoint via HTTP or the same closure.
**Pros:** Solves both issues simultaneously; clean contract; no SSE parsing fragility; enables future non-streaming consumers (tests, integrations).
**Cons:** New endpoint surface; must wire through Aspire service discovery.
**Effort:** Medium.
**Risk:** Low.

## Recommended Action

## Technical Details

- **Affected files:**
  - `GaMcpServer/Tools/ChatTool.cs`
  - `Apps/GaCli/Program.fs` (line 296)
  - `Apps/ga-server/GaApi/` (if adding new endpoint — Option C)

## Acceptance Criteria

- [ ] MCP agents calling the chat tool receive a clean answer string, not raw SSE wire format.
- [ ] `ga ask "what is a tritone substitution?"` and the equivalent MCP tool call return answers routed through the same pipeline (SemanticRouter, all specialized agents).
- [ ] Existing streaming consumers (web client) are unaffected.
- [ ] `dotnet build AllProjects.slnx -c Debug` passes with zero warnings.
- [ ] `dotnet test AllProjects.slnx` passes.

## Work Log

- 2026-03-07 — Identified in /ce:review of PR #2

## Resources

- PR: https://github.com/GuitarAlchemist/ga/pull/2
