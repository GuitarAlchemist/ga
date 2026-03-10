---
status: complete
priority: p2
issue_id: "029"
tags: [agent-native, mcp, skills, code-review]
dependencies: ["009"]
---

# 029 — New Orchestrator Skills Not Exposed as MCP Tools

## Problem Statement

PR #8 adds `ProgressionCompletionSkill` and the SKILL.md-driven `ArpeggioAdvisor`, but neither is exposed as an MCP tool in `GuitaristProblemTools.cs`. This creates an agent-native parity gap: Claude Code and other MCP clients can't invoke these capabilities directly — they must go through the chatbot text interface, which adds LLM overhead and non-determinism.

Related to `009-pending-p1-contextualchords-no-mcp-coverage.md` which covers the broader gap, but this issue is specific to the PR #8 additions.

## Findings

**New skills in PR #8:**
- `ProgressionCompletionSkill` — suggests chord completions for in-progress progressions
- `ArpeggioAdvisor` (SKILL.md) — per-chord arpeggio + mode + target notes

**Existing MCP tool coverage in `GaMcpServer/Tools/GuitaristProblemTools.cs`:**
- `GaArpeggioSuggestions` — exists (used by ArpeggioAdvisor SKILL.md)
- `GaAnalyzeProgression` — exists
- No `GaCompleteProgression` tool

The `ProgressionCompletionSkill` is fully deterministic for key identification (`KeyIdentificationService.ExtractChords` + `Identify`) — the MCP tool could expose the pre-computed diatonic set + top candidates without any LLM call, and let the agent decide what to suggest.

## Proposed Solutions

### Option A — Add `GaCompleteProgression` MCP Tool
```csharp
[McpServerTool, Description("Given a chord progression, identify the key and return diatonic completion candidates")]
public static ProgressionCompletionResult GaCompleteProgression(
    [Description("Space or comma-separated chord symbols, e.g. 'Am F C'")] string chords)
{
    var parsed = KeyIdentificationService.ExtractChords(chords);
    var candidates = KeyIdentificationService.Identify(parsed);
    return new ProgressionCompletionResult(parsed, candidates);
}
```
- **Pros:** Agents can call this directly; no LLM required for deterministic part.
- **Effort:** Small — domain logic already exists in `KeyIdentificationService`.
- **Risk:** Low.

### Option B — Add a `GET /api/chatbot/skills` discovery endpoint
Return the list of registered `IOrchestratorSkill` names, descriptions, and triggers so agents can discover capabilities programmatically.
- **Pros:** General solution — works for all current and future skills.
- **Effort:** Small.
- **Risk:** Low.

### Option C — Defer until 009 is resolved
Treat this as part of the broader MCP coverage gap tracked in `009`.
- **Effort:** None now.
- **Risk:** Gap persists; agents can't use new skills.

## Recommended Action
Option A + Option B — add the `GaCompleteProgression` MCP tool AND the skills discovery endpoint.

## Technical Details

**Files to add/modify:**
- `GaMcpServer/Tools/GuitaristProblemTools.cs` (add `GaCompleteProgression`)
- `Apps/ga-server/GaApi/Controllers/` (add skills discovery endpoint)

## Acceptance Criteria

- [ ] `GaCompleteProgression("Am F C")` MCP tool returns key candidates and diatonic set
- [ ] `/api/chatbot/skills` returns list of registered skills with name, description, triggers
- [ ] Claude Code can invoke progression completion without going through chatbot text

## Work Log

- 2026-03-10: Identified during `/ce:review` of PR #8
