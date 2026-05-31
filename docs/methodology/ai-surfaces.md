---
title: AI surfaces in the GA workspace
status: living
date: 2026-05-16
related:
  - CLAUDE.md (one-line breadcrumb)
  - docs/plans/2026-05-05-tools-antigravity-claude-code-integration-plan.md
  - Scripts/antigravity-bridge.ps1
---

# AI surfaces

This repo is typically opened in **Antigravity** (a VS Code fork). That gives two in-window AI surfaces, with Augment as a third option (extension, in Antigravity or any other VS Code / JetBrains / Vim host).

| Surface | Where | Best for |
|---|---|---|
| **Antigravity native** | bottom-right panel, Claude Opus 4.6 Thinking | quick lookups, single-file edits, sketch-level brainstorming. MCP-capped at 100 tools/instance |
| **Claude Code** | top-right panel, `anthropic.claude-code-2.1.126` | multi-step plans, multi-file refactors, cross-repo work, `/feature`, agent fanout, 1M context window. Reads project-level `.mcp.json` |
| **Augment** | VS Code / JetBrains / Vim extension, Claude Opus 4.7 | semantic "where is X across C#/F#/TS" via codebase index; Linear / Jira / Confluence / Notion without MCP setup; `sub-agent-skill-author` and `sub-agent-skill-graduator` for GA chatbot `SKILL.md` lifecycle |

## Handoff between surfaces

Use [`Scripts/antigravity-bridge.ps1`](../../Scripts/antigravity-bridge.ps1) — drops a note in `state/handoffs/` (gitignored) that the other surface reads on next ask. Documented for Antigravity native ↔ Claude Code; Augment reads/writes the same directory when asked.
