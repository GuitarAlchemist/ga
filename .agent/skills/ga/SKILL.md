---
name: "GA"
description: "Guitar Alchemist developer tools — domain operations, chatbot interaction, DSL evaluation, and performance tracing. Dispatches to sub-commands. Use /ga to see what's available."
---

# /ga — Guitar Alchemist Developer Tools

Use `/ga` when you need to **interact with GA services, domain models, or the chatbot**. Each sub-command is a focused skill with its own guide.

## Sub-Commands

| Command | When to Use |
|---|---|
| `/ga chords` | Parse chord symbols, transpose progressions, get diatonic sets — backed by live GA domain services |
| `/ga eval` | Run GA Language (GAL) scripts and closures via the `GaCli` or the eval endpoint |
| `/ga probe` | Test agent routing decisions, compare responses across agents, send queries to the chatbot API |
| `/ga chat` | Start the full chatbot stack locally, seed data, verify health, trace performance |

## Quick Dispatch

**"Parse Am7"** → `/ga chords`
**"What does `domain.diatonicChords` return for E minor?"** → `/ga eval`
**"Is the chatbot routing this to TheoryAgent?"** → `/ga probe`
**"Start the chatbot locally"** → `/ga chat`
**"Why is the chatbot slow?"** → `/ga chat` (Step 8 — Observability)

## Skill Files

- Sub-command docs: `.agent/skills/ga/<subcommand>/SKILL.md`
- Each skill is self-contained and can be invoked directly: `/ga:chords`, `/ga:eval`, `/ga:probe`, `/ga:chat`

## Architecture Quick Reference

| Layer | Component | Port |
|---|---|---|
| API | GaApi (REST + SignalR + GraphQL) | 5232 / 7184 |
| Chat | Ollama LLM backend | 11434 |
| Data | MongoDB (via Aspire/Docker) | 27017 |
| Tracing | Jaeger | 16686 |
| Monitoring | Aspire Dashboard | 15001 |
| DSL | GaCli (in-process, no server needed) | — |
