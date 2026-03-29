# Mega Session Compound Report — 2026-03-28/29

**Duration**: ~12 hours across 2 days
**Commits**: 35+ in this conversation
**Providers used**: Claude Opus 4.6, Codex GPT-5.4, Gemini 2.5 Pro, Ollama llama3.2

## What Was Built

### 1. Prime Radiant UI (React/TypeScript)
- PlanetNav Godot launch button
- Planet bar declutter (dot strip, hover labels, triage repositioned)
- LLM Status panel (7 providers, real health checks)
- BacklogPanel with AI assessment cards (feasibility, effort, beliefs)
- BrainstormPanel → "Demerzel recommends" (GitHub + CI/CD + governance scan)
- Pipeline action buttons (Brainstorm → Plan → Build → Review → Compound)
- Admin badge next to API Connected status
- Admin-gated pipeline actions
- RailPopover hover cards on icon rail
- Mobile phone layout overhaul (fixed rail z-index 40, top-left button stack, hidden clutter)
- GIS preset fallback + visual feedback
- Godot bridge protocol (GodotBridge.ts + useGodotBridge.ts)
- IXQL epistemic commands (SHOW/METHYLATE/AMNESIA/BROADCAST)

### 2. Epistemic Constitution (Governance)
- 10 articles (E-0 through E-9) — multi-AI brainstorm origin
- CognitionModel JSON schema v1.1 (5 braided strands)
- Policy #19 YAML (epistemic-self-awareness, 40th policy)
- 10 seed strategies with promotion staircase
- Learning state directory

### 3. Demerzel ACP Agent (Python)
- 4 agents: governance, pipeline, epistemic, whats-next
- ACP protocol (acp-sdk 1.0.3, FastAPI/ASGI)
- API key auth middleware (Bearer token + localhost fallback)
- Reads real governance state files
- Ollama LLM backend for reasoning
- GitHub API client for issue/CI scanning

### 4. DemerzelBridge (C# .NET 10)
- MCP server that bridges Claude Code → Demerzel ACP
- 4 MCP tools registered in .mcp.json
- HttpClient with Bearer auth to ACP endpoint
- ModelContextProtocol NuGet package

### 5. Backend Pipeline API (C# ASP.NET)
- PipelineController (POST /api/pipeline/run, /run-all, /active)
- PipelineExecutionService (Claude CLI subprocess + Ollama fallback)
- PipelineHub (SignalR real-time progress)
- Admin-gated (localhost + token)

### 6. Infrastructure
- Blue-green build MSBuild hook (verified working)
- Meshy AI MCP server (built, registered, docs fixed)
- Godot 5-phase integration plan
- BACKLOG.md updated (14 items shipped)
- ForceRadiant lint cleanup (8 unused imports/vars removed)

### 7. Skills
- `/demerzel` — talk to governance agent via ACP
- `demerzel:meta-brainstorm` — recursive meta-learning via Octopus

## Issues Closed: 11
- ga: #30, #32, #33, #34, #36, #37, #38, #39, #40
- ix: #23 (+ PR #24 merged)

## Named Concepts Introduced: 17
1. Epistemic Braiding
2. Contradictory Ground Theorem
3. Teaching-as-Validation
4. Epistemic Viscosity
5. Incompetence Portfolio
6. Deliberate Amnesia
7. Governance Uncertainty Principle
8. Epistemic Tensor (T/F/U/C squared)
9. C_T (Wisdom — stable paradox)
10. T_C (The Hunch)
11. U_F (Blindspot Discovered)
12. Epistemic Epigenetics (methylation)
13. Federated Epistemology
14. Socratic Pathogen
15. Protege Parasite Protocol
16. Abductive Ascent
17. Introspection Perturbation

## Architecture Established

```
Claude Code ──MCP/stdio──→ DemerzelBridge (.NET) ──HTTP/ACP──→ Demerzel Agent (Python:8200)
                                                                    │
Codex/Gemini ──ACP client──────────────────────────────────────────→│
                                                                    │
Prime Radiant ──HTTP──→ GaApi (.NET) ──→ PipelineController         │
    │                        │                                      │
    │                        └──→ SignalR PipelineHub               │
    │                                                               │
    └──IXQL──→ epistemic commands ──→ visual tensor on 3D graph     │
                                                                    │
                                                              governance/demerzel/
                                                                state/beliefs/
                                                                state/strategies/
                                                                state/learning/
                                                                constitutions/
                                                                policies/
```

## Key Decisions

| Decision | Choice | Why |
|----------|--------|-----|
| Epistemic architecture | Braided (not layered) | Meta-cognition runs alongside object-cognition, not above it |
| Regress halting | Contradictory Ground Theorem | C is a fixed point under meta-reflection — principled, not arbitrary |
| Strategy management | Methylation (not deletion) | Biological epigenetics — suppress without losing, reversible |
| Agent protocol | ACP (Agent Communication Protocol) | Open standard, Python SDK available, REST-based discovery |
| Claude Code bridge | MCP stdio server (.NET) | Reuses existing MCP infrastructure, no new dependencies |
| Auth | API key + localhost bypass | Simple, appropriate for single-owner system |
| Pipeline execution | Claude CLI + Ollama fallback | Best available LLM, graceful degradation |
| Mobile layout | Fixed rail z-index 40 | Nothing can cover the primary navigation |

## What's Next (for future sessions)

| Priority | Item | Effort |
|----------|------|--------|
| **High** | ix ACP agent (Rust ML, embeddings) | M |
| **High** | TARS ACP agent (F# reasoning) | M |
| **High** | GA ACP agent (music theory DSL) | M |
| **High** | Seldon ACP agent (predictions) | M |
| **Medium** | Wire pipeline to real Claude Code skills (/octo:brainstorm, /ce:plan) | M |
| **Medium** | Roles/entitlements system (when multi-user) | M |
| **Medium** | Godot Phase 2: Constitutional Gravity Engine | L |
| **Low** | Streeling learner models (per-agent cognition profiles) | L |
| **Low** | Deliberate amnesia scheduler (automated, not manual) | S |
| **Low** | Belief durability stress tests (adversarial probes) | M |

## Patterns Worth Repeating

1. **Multi-AI brainstorm for architecture decisions** — 4 providers produced concepts no single model would have. The Epistemic Constitution is genuinely multi-perspective.
2. **Ship the skill + the infra together** — the `/demerzel` skill and the ACP agent shipped in the same session. Users can use it immediately.
3. **ACP + MCP bridge pattern** — reusable for ix, TARS, GA, Seldon. Build the ACP agent in the domain's native language, bridge to Claude Code via MCP.
4. **Mobile-first fixes** — phone layout broke multiple times. Fixed rail with z-index 40 is the pattern.
5. **Admin check** — hostname + domain + token, checked in both frontend and backend.

## Mistakes to Avoid

1. **Don't use `position: flex` for mobile navigation** — it gets pushed off-screen by dynamic content. Use `position: fixed`.
2. **Don't forget submodule push** — Demerzel is a submodule, needs separate commit + push before parent ref update.
3. **Check ACP SDK version** — Python 3.14 broke `Server.run()`. Use `create_app()` + uvicorn instead.
4. **Admin check must include tunnel domains** — `demos.guitaralchemist.com` is the owner's domain, not just localhost.
