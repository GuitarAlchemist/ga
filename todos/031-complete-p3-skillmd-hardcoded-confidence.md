---
status: complete
priority: p3
issue_id: "031"
tags: [code-quality, skillmd, confidence, code-review]
dependencies: []
---

# 031 — `SkillMdDrivenSkill` Hardcodes Confidence = 0.9f Regardless of Response Quality

## Problem Statement

`SkillMdDrivenSkill.ExecuteAsync` always returns `Confidence = 0.9f` even when the LLM response is empty, truncated, or clearly incomplete. The `SemanticRouter` and telemetry consume `AgentResponse.Confidence` for routing decisions and dashboards — a hardcoded constant degrades these signals to noise.

## Findings

`Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs` lines 107–112:
```csharp
return new AgentResponse
{
    AgentId    = $"skill.md.{skillMd.Name.ToLowerInvariant().Replace(' ', '-')}",
    Result     = text,
    Confidence = 0.9f,   // ← always 0.9, even for empty/truncated responses
};
```

By contrast, the domain skills (`KeyIdentificationSkill`, `ProgressionCompletionSkill`) call `ParseStructuredResponse` which extracts `confidence` from the LLM's own JSON output. `SkillMdDrivenSkill` could do the same — the Anthropic response sometimes contains confidence-like signals in structured outputs.

## Proposed Solutions

### Option A — Use response length as a proxy heuristic
```csharp
var confidence = text.Length switch
{
    0     => 0f,
    < 50  => 0.5f,
    _     => 0.9f,
};
```
Simple, no LLM call change needed.

### Option B — Attempt to parse confidence from the response text
If the SKILL.md body instructs the LLM to respond with JSON containing a `confidence` field, parse it using the existing `ParseStructuredResponse` approach.

### Option C — Accept 0.9f for now, document it
Add a code comment explaining the hardcoded value is an intentional placeholder.

## Recommended Action
Option C for now — the hardcoded value is a minor quality issue. Revisit if confidence signals drive actual routing decisions.

## Acceptance Criteria

- [ ] Code comment added explaining why `Confidence = 0.9f` is a placeholder
- [ ] If/when confidence signals drive routing, this is revisited with Option A or B

## Work Log

- 2026-03-10: Identified during `/ce:review` of PR #8
