# Gemini CLI Configuration

This repository uses Gemini CLI for AI-assisted development tasks.

## Planning System

Active planning lives in two places:

- **`BACKLOG.md`** at the repo root — future ideas, one bullet per idea; remove when a plan is created
- **`docs/plans/`** — per-feature plans (authoritative, one file per feature, `status: active | completed`)

Historical planning artifacts (Conductor tracks, Nov 2025 Roadmap) are in `docs/archive/`. They are read-only reference.

## Architecture Quick Reference

See `CLAUDE.md` for the full architecture guide, build commands, and coding standards.

| Layer | Projects |
|---|---|
| Core | `GA.Core`, `GA.Domain.Core` |
| Domain | `GA.Business.Core`, `GA.Business.Config`, `GA.BSP.Core` |
| Analysis | `GA.Business.Core.Harmony`, `GA.Business.Core.Fretboard` |
| AI/ML | `GA.Business.ML` |
| Orchestration | `GA.Business.Core.Orchestration`, `GA.Business.Intelligence` |

## Tech Stack

- **.NET 10 / C# 14** — all backend projects
- **F#** — `GA.Business.DSL`, `GA.Business.Config`
- **React 18 / Vite / TypeScript** — `Apps/ga-client`
- **MongoDB + Qdrant** — vector search backends
- **Ollama** — local LLM inference (chatbot + embeddings)
- **Aspire** — local service orchestration
