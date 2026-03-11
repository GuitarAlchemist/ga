---
status: complete
priority: p1
issue_id: "035"
tags: [agent-native, mcp, skills, discovery, code-review]
dependencies: ["029"]
---

# 035 — Active Skill Registry Not Discoverable by Agents

## Problem Statement

`SkillMdPlugin` discovers and registers `SkillMdDrivenSkill` instances at startup, but there is no `GET /api/chatbot/skills` endpoint or MCP tool that lets an agent query "what skills are currently active and what triggers do they match?" An agent asked to invoke the arpeggio advisor programmatically has no way to enumerate the live skill registry — it must guess trigger strings from source files.

## Findings

`SkillMdPlugin.Register()` populates the DI container with `IOrchestratorSkill` singletons. The registered skills include their `Name`, `Description`, and `Triggers` from the SKILL.md frontmatter. None of this metadata is exposed externally.

`GaMcpServer/Tools/ChatTool.cs` — `AskChatbot` MCP tool exists but takes raw text, not a skill-targeted invocation.

No controller in `GaApi/Controllers/` exposes a skills list endpoint.

## Proposed Solutions

### Option A — Add REST endpoint + MCP tool (Recommended)
```csharp
// GaApi/Controllers/SkillsController.cs
[HttpGet("/api/chatbot/skills")]
public IActionResult ListSkills([FromServices] IEnumerable<IOrchestratorSkill> skills)
    => Ok(skills.Select(s => new { s.Name, s.Description, s.Triggers }));
```
```csharp
// GaMcpServer/Tools/ChatTool.cs
[McpServerTool, Description("List all active chatbot skills with their trigger phrases")]
public static async Task<object> ListChatbotSkills(HttpClient http)
    => await http.GetFromJsonAsync<object>("/api/chatbot/skills");
```
- **Effort:** Small.
- **Risk:** Low — read-only endpoint.

### Option B — Expose via existing `/api/chatbot/status` or health endpoint
Append skills list to the health check response.
- **Effort:** Trivial.
- **Cons:** Mixes operational and capability metadata.

### Option C — Document trigger strings in CLAUDE.md
No code change; agents read the doc.
- **Effort:** Trivial.
- **Cons:** Goes stale as SKILL.md files are added/removed; breaks the agent-native principle.

## Recommended Action
Option A.

## Acceptance Criteria

- [ ] `GET /api/chatbot/skills` returns JSON array with `name`, `description`, `triggers` for each active skill
- [ ] `ListChatbotSkills` MCP tool returns same data
- [ ] Skills list updates dynamically when `SKILLMD_SKILLS_PATH` changes (or at least reflects startup state)
- [ ] Claude Code can list active skills without reading source files

## Work Log

- 2026-03-10: Identified during agent-native review of PR #8
