---
status: complete
priority: p1
issue_id: "026"
tags: [security, error-handling, skillmd, code-review]
dependencies: []
---

# 026 — `SkillMdDrivenSkill` Leaks Raw `ex.Message` in Agent Response

## Problem Statement

When an `AnthropicClient` call fails, `SkillMdDrivenSkill` returns the raw exception message directly in the `AgentResponse.Result` that is shown to the end user. This can expose internal details: API key validation error text, HTTP response bodies from Anthropic, internal service addresses, or stack-trace fragments embedded in message strings.

## Findings

`Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs` lines 116–122:
```csharp
catch (Exception ex) when (ex is not OperationCanceledException)
{
    logger.LogError(ex, "SkillMdDrivenSkill [{Skill}] failed", skillMd.Name);
    return new AgentResponse
    {
        AgentId    = $"skill.md.{skillMd.Name.ToLowerInvariant().Replace(' ', '-')}",
        Result     = $"I encountered an error processing your request: {ex.Message}",  // ← leaks
        Confidence = 0f,
    };
}
```

Example leaks:
- `AnthropicClient` auth failure: `"401 Unauthorized: invalid x-api-key"` — confirms API key is present but wrong
- MCP tool timeout: `"The operation timed out: https://internal-mcp-server.local:8080/tools"` — leaks internal URL
- Anthropic rate limit: `"429 Too Many Requests: exceeded rate limit for claude-sonnet-4-6"` — leaks model name and limit

Note: `ex.Message` alone (without stack trace) is lower severity than `ex.ToString()`, but still qualifies as information disclosure. Compare to `020-pending-p3-ex-message-leaked-http-500.md` which covers controller catch blocks; this is the skill execution path.

## Proposed Solutions

### Option A — Return generic message, log full exception (Recommended)
```csharp
logger.LogError(ex, "SkillMdDrivenSkill [{Skill}] failed", skillMd.Name);
return new AgentResponse
{
    AgentId    = $"skill.md.{skillMd.Name.ToLowerInvariant().Replace(' ', '-')}",
    Result     = "I'm having trouble processing your request right now. Please try again.",
    Confidence = 0f,
};
```
- **Pros:** Zero information disclosure; full exception already in logs.
- **Effort:** Trivial — one-line change.
- **Risk:** None.

### Option B — Expose message only in Development
```csharp
var userMsg = env.IsDevelopment() ? $"Error: {ex.Message}" : "I'm having trouble right now. Please try again.";
```
- **Pros:** Helpful in local dev.
- **Cons:** Requires injecting `IHostEnvironment` (adds constructor parameter).
- **Effort:** Small.

## Recommended Action
Option A — one-line fix, consistent with existing `HealthController` pattern.

## Technical Details

**Affected file:** `Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs` line 121

## Acceptance Criteria

- [ ] `AgentResponse.Result` never contains raw `ex.Message` on error
- [ ] Full exception is still logged via `logger.LogError`
- [ ] User receives a generic retry message

## Work Log

- 2026-03-10: Identified during `/ce:review` of PR #8
