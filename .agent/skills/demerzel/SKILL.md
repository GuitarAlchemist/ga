---
name: demerzel
description: "Talk to Demerzel — the AI governance agent. Query beliefs, run pipelines, execute epistemic commands, and get recommendations via ACP."
allowed-tools: Read, Write, Edit, Bash, Grep, Glob
---

# Demerzel — Governance Agent Skill

Talk to Demerzel via the ACP (Agent Communication Protocol) bridge.
Demerzel runs as a standalone agent on port 8200 with 4 sub-agents.

## Prerequisites

1. **Demerzel ACP server running:**
   ```bash
   cd Apps/demerzel-agent && DEMERZEL_API_KEY=mykey uvicorn src.server:app --port 8200
   ```
2. **Bridge built:**
   ```bash
   dotnet build Apps/demerzel-bridge/DemerzelBridge/DemerzelBridge.csproj
   ```
3. **Registered in `.mcp.json`** (already done — `"demerzel"` entry)

## Commands

Parse the user's input and route to the correct sub-command:

| User says | Route to | MCP tool |
|-----------|----------|----------|
| `/demerzel beliefs` or "list beliefs" | Governance | `demerzel_governance` |
| `/demerzel policies` | Governance | `demerzel_governance` |
| `/demerzel constitution [name]` | Governance | `demerzel_governance` |
| `/demerzel strategies` | Governance | `demerzel_governance` |
| `/demerzel brainstorm <title>` | Pipeline | `demerzel_pipeline` |
| `/demerzel plan <title>` | Pipeline | `demerzel_pipeline` |
| `/demerzel build <title>` | Pipeline | `demerzel_pipeline` |
| `/demerzel review <title>` | Pipeline | `demerzel_pipeline` |
| `/demerzel compound <title>` | Pipeline | `demerzel_pipeline` |
| `/demerzel run all <title>` | Pipeline (full) | `demerzel_pipeline` |
| `/demerzel show tensor` | Epistemic | `demerzel_epistemic` |
| `/demerzel show beliefs where ...` | Epistemic | `demerzel_epistemic` |
| `/demerzel methylate <id> [reason]` | Epistemic | `demerzel_epistemic` |
| `/demerzel demethylate <id>` | Epistemic | `demerzel_epistemic` |
| `/demerzel amnesia <belief> [in N days]` | Epistemic | `demerzel_epistemic` |
| `/demerzel broadcast` | Epistemic | `demerzel_epistemic` |
| `/demerzel whats next` or `/demerzel` (no args) | What's Next | `demerzel_whats_next` |
| Any other text | What's Next (with query) | `demerzel_whats_next` |

## Execution

### Step 1 — Check if Demerzel is running

```bash
curl -sf http://localhost:8200/agents | head -1 && echo "OK" || echo "NOT RUNNING"
```

If not running, tell the user:
> Demerzel ACP agent is not running. Start it with:
> ```bash
> cd Apps/demerzel-agent && uvicorn src.server:app --port 8200
> ```

### Step 2 — Route the command

If the MCP tool `demerzel_governance` / `demerzel_pipeline` / `demerzel_epistemic` / `demerzel_whats_next` is available, call it directly.

If MCP tools are not available (bridge not running), fall back to direct HTTP:

```bash
curl -s -X POST http://localhost:8200/runs \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${DEMERZEL_API_KEY}" \
  -d '{"agent_name": "<agent>", "input": [{"parts": [{"content": "<message>", "content_type": "text/plain"}]}]}'
```

### Step 3 — Present the response

- **Governance queries**: Format JSON responses as tables or bullet lists
- **Pipeline stages**: Show each stage result with timing
- **Epistemic commands**: Show tensor distributions, methylation confirmations, amnesia schedules
- **What's Next**: Present as prioritized cards (urgent/high/quick/strategic)

### Step 4 — Offer follow-ups

After each command, suggest related actions:

- After `list beliefs` → "Try `show tensor` to see the epistemic distribution"
- After `whats next` → "Pick an item and run `brainstorm: <title>` to start"
- After `brainstorm` → "Run `plan: <title>` to create an implementation plan"
- After pipeline complete → "Run `compound: <title>` to document learnings"

## Architecture

```
/demerzel skill
    │
    ├── MCP path: Claude Code → DemerzelBridge (MCP/stdio) → ACP HTTP → Demerzel Agent
    │
    └── HTTP fallback: Claude Code → curl → ACP HTTP → Demerzel Agent
```

## Epistemic Constitution Reference

The epistemic commands implement Articles E-0 through E-9:

| Article | Command |
|---------|---------|
| E-1 Contradictory Ground | `show beliefs where truthState = contradictory` |
| E-3 Epistemic Viscosity | `show beliefs where viscosity > 0.8` |
| E-5 Deliberate Amnesia | `amnesia <belief> in 30 days` |
| E-7 Epistemic Tensor | `show tensor` |
| E-8 Epigenetics | `methylate <strategy> reason ...` |
| E-9 Federated Review | `broadcast beliefs` |

## Examples

```
/demerzel
→ Scans GitHub + CI/CD + governance, returns prioritized recommendations

/demerzel beliefs
→ Lists all tetravalent belief states (T/F/U/C)

/demerzel show tensor
→ Shows epistemic tensor distribution (wisdom, hunches, blindspots)

/demerzel brainstorm: Godot bridge protocol
→ Runs brainstorm stage via Ollama, returns 3-5 ideas

/demerzel run all: Fix CI failures
→ Runs full pipeline: brainstorm → plan → build → review → compound

/demerzel methylate socratic_pathogen reason caused learner distress
→ Suppresses the strategy (Article E-8)

/demerzel amnesia old-architecture-assumption in 14 days
→ Schedules belief for deletion test (Article E-5)
```
