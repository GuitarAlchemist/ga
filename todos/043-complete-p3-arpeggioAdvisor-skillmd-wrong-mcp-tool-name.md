---
status: complete
priority: p3
issue_id: "043"
tags: [agent-native, skills, mcp, skillmd, code-review]
dependencies: []
---

# 043 — ArpeggioAdvisor SKILL.md References Wrong MCP Tool Name

## Problem Statement

The Arpeggio Advisor SKILL.md system prompt instructs the LLM to "call `GaAnalyzeProgression`" as a step in its workflow. `GaAnalyzeProgression` is not a registered MCP tool name — the actual tool for progression analysis in `GuitaristProblemTools.cs` is `GaProgressionCompletion` (or `GaCompleteProgression` depending on the version). When `SkillMdDrivenSkill` loads this SKILL.md and the LLM attempts a tool call with `GaAnalyzeProgression`, it will receive a tool-not-found error silently or return an unhelpful response.

## Findings

`.agent/skills/arpeggio-advisor/SKILL.md` line 26 (approximate): instructs LLM to call `GaAnalyzeProgression`.

`GaMcpServer/Tools/GuitaristProblemTools.cs`: registered tools include `GaArpeggioSuggestions`, `GaProgressionCompletion`, `GaKeyFromProgression` — no `GaAnalyzeProgression`.

## Proposed Solutions

### Option A — Audit and fix the SKILL.md tool name
Verify the intended tool, update the SKILL.md system prompt to use the correct registered MCP tool name.
- **Effort:** Trivial — edit one line in SKILL.md.

### Option B — Register an alias tool `GaAnalyzeProgression` that delegates to the real tool
- **Effort:** Small.
- **Cons:** Bloats the tool registry.

## Recommended Action
Option A — fix the SKILL.md to reference the correct tool name.

## Acceptance Criteria

- [ ] Arpeggio Advisor SKILL.md references a tool name that exists in `GuitaristProblemTools.cs`
- [ ] Integration test or manual probe: arpeggio advisor successfully invokes the progression tool
- [ ] No tool-not-found errors in Arpeggio Advisor execution logs

## Work Log

- 2026-03-10: Identified during agent-native review agent for PR #8
