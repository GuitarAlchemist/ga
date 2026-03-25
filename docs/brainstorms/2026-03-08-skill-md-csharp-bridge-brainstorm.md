# Brainstorm: SKILL.md → C# Bridge (SkillMdDrivenSkill)

**Date:** 2026-03-08
**Status:** Ready for planning
**Author:** AI + Stephane Pareilleux

---

## What We're Building

A `SkillMdDrivenSkill` that loads any `.agent/skills/**/*.md` file at runtime and wraps it as a live `IOrchestratorSkill`, backed by **Claude API** (Anthropic C# SDK) with **GA domain tools** available via the **MCP C# SDK** in-process.

This closes the loop between:
- **Claude Code world** — `.agent/skills/` SKILL.md files that guide Claude
- **C# world** — `IOrchestratorSkill` instances that run in the production chatbot

With this bridge, a new chatbot skill can be prototyped purely in markdown, tested live in the chatbot, then gradually replaced with optimized domain-coded C# when the pattern is proven.

---

## Why This Approach

Three options were considered:

| Option | Description | Decision |
|--------|-------------|----------|
| A — Anthropic Skills API | Use hosted Skills API + official C# SDK | Rejected: skills must be version-controlled locally, not hosted |
| **B — Local SKILL.md loader** | Parse `.agent/skills/` files, use body as system prompt, bind tools via MCP C# SDK | **Selected** |
| C — Semantic Kernel | SK as host, GA tools as SK plugins | Rejected: overkill, moves away from Claude-native patterns |

**Option B fits GA because:**
- 19 SKILL.md files already exist in `.agent/skills/` — zero new authoring work to test
- `GaMcpServer` already uses `ModelContextProtocol` SDK and exposes all domain tools
- `McpClientTool : AIFunction` connects directly to any `IChatClient`
- Anthropic C# SDK is faster and more capable than local Ollama for the LLM step
- The entire stack is one coherent MCP story: Claude + MCP tools, same as Claude Code itself

---

## Key Decisions

### 1. LLM: Anthropic C# SDK (not Ollama)
Ollama is too slow for synchronous chatbot responses. The `SkillMdDrivenSkill` will use the Anthropic C# SDK with `claude-sonnet-4-6` (or configurable model). This requires `ANTHROPIC_API_KEY` in environment/config.

### 2. Tool Binding: MCP C# SDK in-process
`GaMcpServer` is already tagged with `[McpServerTool]` attributes using `ModelContextProtocol v0.1.0-preview.10`. We host the same tools in-process via `services.AddMcpServer().WithTools<GaDslTool>()...`, then surface them as `McpClientTool[]` (which are `AIFunction[]`) passed to the `IChatClient`. Zero subprocess, zero network.

### 3. Trigger Matching: SKILL.md frontmatter `triggers` list
An optional `triggers` field in YAML frontmatter declares keyword patterns for `CanHandle()`. Skills with no `triggers` are discoverable but not auto-routed (must be invoked explicitly via `/ga eval` or similar). This is explicit, version-controlled, and reviewable.

```yaml
---
name: "GA Chords"
description: "Parse chords, transpose progressions, diatonic sets"
triggers:
  - "transpose"
  - "diatonic chords"
  - "parse chord"
  - "what chords are in"
---
```

### 4. Execution Model: Agentic Loop (not single-shot)
The SKILL.md body is injected as the system prompt. The Anthropic SDK runs a multi-turn tool-use loop: Claude reasons → calls GA MCP tools → observes results → produces final answer. This mirrors exactly how Claude Code uses SKILL.md today.

### 5. Graduation Path: SKILL.md → optimized C#
The development lifecycle:
1. Write `SKILL.md` with `triggers` → auto-loaded as `SkillMdDrivenSkill`
2. Test via chatbot or `GaChatbotCli` — refine the markdown
3. When stable, scaffold C# skeleton: `ga skill scaffold .agent/skills/foo/SKILL.md`
4. Replace with domain-optimized `IOrchestratorSkill` (pure domain, 0 LLM calls)
5. Remove `triggers` from SKILL.md (now only a Claude Code guide again)

---

## Architecture

```
ChatRequest.Message
      │
      ▼
ProductionOrchestrator
  foreach IOrchestratorSkill:
    ├── ScaleInfoSkill          (pure domain, 0 LLM)
    ├── FretSpanSkill           (pure domain, 0 LLM)
    ├── ChordSubstitutionSkill  (Grothendieck, 0 LLM)
    ├── KeyIdentificationSkill  (domain + Ollama)
    └── SkillMdDrivenSkill[]   ← NEW (one per SKILL.md with triggers)
              │
              ▼
        SKILL.md body → System prompt
        triggers      → CanHandle()
              │
              ▼
        Anthropic C# SDK (claude-sonnet-4-6)
              │  ←→ tool calls
              ▼
        MCP C# SDK (in-process)
          GaDslTool.GaParseChord()
          GaDslTool.GaDiatonicChords()
          KeyTools.GetKeyNotes()
          ... all GaMcpServer tools
```

---

## Components to Build

| Component | Location | Notes |
|-----------|----------|-------|
| `SkillMd` record | `GA.Business.ML/Skills/SkillMd.cs` | Frontmatter + body, parsed from SKILL.md |
| `SkillMdParser` | `GA.Business.ML/Skills/SkillMdParser.cs` | YAML frontmatter (YamlDotNet, existing pattern) + markdown body |
| `SkillMdDrivenSkill` | `GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs` | `IOrchestratorSkill` backed by Anthropic SDK + MCP tools |
| `SkillMdLoader` | `GA.Business.ML/Skills/SkillMdLoader.cs` | Scans directory, returns `IReadOnlyList<SkillMdDrivenSkill>` |
| `AddSkillMdSkills()` | `ChatbotOrchestrationExtensions.cs` | DI registration — scans `.agent/skills/` for files with `triggers` |
| MCP in-process hosting | `ChatbotOrchestrationExtensions.cs` | `services.AddMcpServer().WithTools<GaDslTool>()...` |
| `GaChatbotCli` (optional) | `Apps/GaChatbotCli/` | CLI that runs the skill runner in-process, called by Claude Code skill |
| `ga skill scaffold` (optional) | `Apps/GaCli/Program.fs` | Generates C# `IOrchestratorSkill` skeleton from SKILL.md |

---

## Open Questions

*(Resolved during brainstorm — none remaining)*

### Resolved Questions

| Question | Decision |
|----------|----------|
| Tool binding: in-process or HTTP? | In-process via MCP C# SDK — zero network overhead |
| Trigger matching: explicit or inferred? | Explicit `triggers` in SKILL.md frontmatter |
| LLM provider: Ollama or Anthropic? | Anthropic C# SDK — faster and avoids Ollama latency |
| MCP SDK: custom or official? | Official `modelcontextprotocol/csharp-sdk` (already used by GaMcpServer) |
| Which MCP tools to expose? | All GaMcpServer tools except blocked categories (`io.*`, `agent.*`, `tab.*` from firewall) |

---

## What Success Looks Like

```bash
# 1. Author a new skill in markdown — no C# needed
cat .agent/skills/ga/chords/SKILL.md
# (add triggers: ["parse chord", "transpose"])

# 2. The chatbot auto-loads it and routes matching messages
curl -s -X POST http://localhost:5232/api/chatbot/chat \
  -d '{"message": "Parse Am7 for me"}' | jq '.routing'
# → { "agentId": "skill-md.GA Chords", "routingMethod": "orchestrator-skill" }

# 3. Claude + GA domain tools answer it correctly
# → "Am7 = A minor 7th: root A, minor 3rd C, perfect 5th E, minor 7th G"

# 4. GaChatbotCli (optional): test without Aspire
dotnet run --project Apps/GaChatbotCli -- "parse Am7"
# → same answer, in-process, no server

# 5. Once stable, scaffold to C#
dotnet run --project Apps/GaCli -- skill scaffold .agent/skills/ga/chords/SKILL.md
# → Common/GA.Business.ML/Agents/Skills/ChordsSkill.cs (skeleton)
```

---

## Dependencies to Add

| Package | Project | Purpose |
|---------|---------|---------|
| `Anthropic` (official C# SDK) | `GA.Business.ML` | Claude API calls in `SkillMdDrivenSkill` |
| `ModelContextProtocol` (update to latest) | `GA.Business.ML`, `ChatbotOrchestrationExtensions` | In-process MCP server + client |
| `YamlDotNet` | already present | SKILL.md frontmatter parsing |
