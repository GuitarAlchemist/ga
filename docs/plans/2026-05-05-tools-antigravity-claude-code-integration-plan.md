---
title: Antigravity ↔ Claude Code integration
date: 2026-05-05
type: tools
reversibility: two-way door (config + docs only)
revisit_trigger: when a third AI surface joins (Cursor, etc.) OR when the 181-tool overflow blocks real work
status: draft
---

# Antigravity ↔ Claude Code integration

## Why this exists

Working in this repo currently means *two* AI surfaces sharing one editor:

1. **Antigravity native AI** — bottom-right panel, Claude Opus 4.6 Thinking, has its own MCP loader. Currently throwing an MCP Error: *"adding this instance with 181 enabled tools would exceed max limit of 100."*
2. **Claude Code extension** (`anthropic.claude-code-2.1.126`) — top-right panel, Spark icon, regular VS Code extension running inside Antigravity (which is a VS Code fork). Reads project-level `.mcp.json`.

Neither surface is aware of what the other is doing. The shared workspace is the only coordination point — both can read files, both can run terminals, but the two chats don't see each other's history.

This document maps what's where, the immediate blocker, and the integration angles worth investing in.

## What feeds which surface

| Layer | Surface | Source of truth | What it loads |
|---|---|---|---|
| Antigravity native AI | Antigravity panel | `C:/Users/spare/AppData/Roaming/Antigravity/User/mcp.json` | Just `GitKraken` at user level today; the rest of the 181-tool count comes from Antigravity built-ins + extension contributions (not auditable from outside the running process) |
| Claude Code extension | top-right Spark panel | `<repo>/.mcp.json` (project-level) | This repo: `ix`, `meshy-ai`, `demerzel`, `paper-search`, `chrome-devtools` |
| Claude Code CLI (terminal) | terminal | Same `<repo>/.mcp.json` + user `~/.claude/settings.json` | Same as the extension when run from the same workspace |

The Claude Code extension is a straight VS Code extension targeting `vscode ^1.94.0`. It contributes 21 commands under `claude-vscode.*`, 7 keybindings, a webview, and a walkthrough. Antigravity is a VS Code fork and runs it as-is.

## Immediate blocker — MCP tool budget

Antigravity is hard-capped at 100 enabled MCP tools per AI instance. With 181 currently enabled, *some are being silently dropped*. Until this is resolved, every Antigravity AI session is running with a degraded, non-deterministic toolset.

**Action needed (your call, not mine to execute)**:

1. From Antigravity's MCP UI, list every connected server and its tool count.
2. Decide which 81+ tools you can drop. Heuristic order:
   - Servers that duplicate functionality (e.g. multiple GitHub-shaped MCPs)
   - Servers whose tools you've never invoked in the last 30 days
   - Servers that are research-mode only (paper-search, etc.) — keep enabled only when needed
3. Where a server is genuinely useful only sometimes, prefer **disable + re-enable on demand** over leaving it loaded.
4. Where the same server is useful in both surfaces, prefer leaving it on Claude Code (project-level `.mcp.json`) and *off* on Antigravity native — Claude Code has no 100-tool cap.

This is reversible config — try cuts, observe what breaks. The blast radius is "the AI temporarily can't call X."

## Integration angles, smallest first

### 1. Surface routing (already implicit)

You already do this informally: ad-hoc questions go to Antigravity native AI; deep multi-step work goes to Claude Code. Make it explicit:

- **Antigravity native** — quick lookups, single-file edits, "what does this file do," sketch-level brainstorming.
- **Claude Code** — multi-step plans, multi-file refactors, cross-repo work, anything that needs `/feature`, agent fanout, or the 1M context window.

Cost: zero. Just discipline. Worth writing into `CLAUDE.md` so future-Claude knows.

### 2. Project-level MCP separation (config-only, recoverable)

Move servers that *only* Claude Code uses out of any user-level Antigravity config. Keep them in `<repo>/.mcp.json` so Claude Code still loads them when you open the workspace, but they don't count against Antigravity's 100-tool cap.

Status: `.mcp.json` already does this for `ix`, `meshy-ai`, `demerzel`, `paper-search`, `chrome-devtools`. Verify the same servers aren't *also* in Antigravity's list — if they are, drop them from there.

### 3. Claude Code as Antigravity's "deep work" handoff

When Antigravity native hits a multi-step task, have it write a brief into a known location (e.g. `state/handoffs/<timestamp>.md`) and prompt the user to spawn a Claude Code session. Inverse: Claude Code session results land in the same dir and Antigravity reads them on next ask.

Cost: a folder + a convention. Low. Useful only if you actually do the handoff often enough to justify it.

### 4. Antigravity-MCP server (speculative — needs Antigravity-side investigation)

If Antigravity exposes an MCP server for *its own* IDE state — open tabs, current selection, diagnostics, terminal output, agent task list — Claude Code could connect to it and gain situational awareness it can't get from file-reads alone.

I don't know whether this exists. To find out:

- Check Antigravity's settings/docs for "MCP server" or "expose IDE state."
- Check the `extensions.json` and look for any extension that exposes a local socket / port.
- If it doesn't exist: file-system handoff (option 3) is the cheap substitute.

### 5. Slash-command bridge (speculative — see #4)

If Antigravity has commands you invoke often (Spark workflows, etc.), wrap them as Claude Code slash commands that shell out via `code --command <name>` (or whatever the Antigravity equivalent is). One muscle memory instead of two.

Cost: per-command, ~30 min each. Worth it only for commands you use multiple times a day.

## What I won't recommend

- **Forcing both AIs to share chat history.** Two AIs with overlapping memory but distinct personas usually produces worse outputs than two clean instances. Keep their contexts separate.
- **Replacing one with the other.** Antigravity native is faster for casual asks; Claude Code is deeper for multi-step work. Keep both.
- **Auto-tool-trimming via heuristics.** Which 81 tools to drop is a judgment call about your workflow, not something an agent can optimize blindly.

## Ordered next steps

1. **Audit the 181** — open Antigravity's MCP UI, screenshot or copy the server list, sort by tool count. (~10 min, blocking.)
2. **Cut to ≤ 100** — disable the deltas. Re-test the AI surfaces with a real prompt to confirm nothing critical broke. (~30 min.)
3. **Write the surface-routing rule** into [CLAUDE.md](../../CLAUDE.md) and [AGENTS.md](../../AGENTS.md). (~5 min.)
4. **Check for an Antigravity MCP server** — search docs / settings / known port-binders. (~30 min, speculative.)
5. **Pick one slash-command bridge** if (4) shows promise. (~30 min trial.)

Steps 1–3 are clear wins. Steps 4–5 are exploration; do them only if (1–3) leave you wanting deeper integration.

## What this plan deliberately does NOT do

- Touch any user-level Antigravity config (those can hold tokens; agent shouldn't read or write them without explicit per-file authorization).
- Trim or namespace MCP servers automatically — destructive to the running tool topology, judgment call.
- Promise integrations whose API surface I haven't verified (the speculative items are clearly marked).
