# Guitar Alchemist — Agent Configuration

This repository uses a **hybrid workflow**:
- **Conductor** (context layer) — product definition, tech stack, track registry, institutional memory
- **Compound Engineering** (execution layer) — plan → work → review → compound cycle

---

## Conductor Context Layer

Project context, architecture decisions, and track tracking.

**[→ Conductor Index](conductor/index.md)**

### Quick Links
- [Track Registry](conductor/tracks.md)
- [Tech Stack](conductor/tech-stack.md)
- [Product Definition](conductor/product.md)
- [Dev Workflow & Commands](conductor/workflow.md)

---

## Compound Engineering Execution Layer

Day-to-day execution workflow. Use these commands to drive work on any track.

**[→ Compound Engineering Config](compound-engineering.local.md)**

### Workflow
```
/ce:plan → /ce:work → /ce:review → /ce:compound → repeat
```

### Commands
| Command | What it does |
|---|---|
| `/ce:brainstorm <idea>` | Structured ideation before planning |
| `/ce:plan <feature>` | Creates `docs/plans/YYYY-MM-DD-<type>-<name>-plan.md` |
| `/ce:work <plan-file>` | Executes the plan with todo tracking + incremental commits |
| `/ce:review` | Runs security, performance, architecture reviewer agents |
| `/ce:compound` | Captures learnings into `docs/solutions/` |

### Working Artifacts
- **Brainstorms:** `docs/brainstorms/` — raw exploration docs
- **Plans:** `docs/plans/` — structured work plans (output of `/ce:plan`)
- **Solutions:** `docs/solutions/` — captured learnings (output of `/ce:compound`)

---

## Active Tracks → Plans

Drive Conductor tracks through the Compound Engineering workflow:

| Track | Status | Next Action |
|---|---|---|
| `spectral-rag-chatbot` | Active | `/ce:plan spectral RAG chatbot remaining work` |
| `modernization` | Active | `/ce:plan modernization remaining items` |
| `semantic-event-routing` | Proposed | `/ce:brainstorm semantic event routing` |
| `core-schema-design` | Needs Reconciliation | `/ce:plan core schema reconciliation` |
| `meai-integration` | Needs Reconciliation | `/ce:plan MEAI integration reconciliation` |
