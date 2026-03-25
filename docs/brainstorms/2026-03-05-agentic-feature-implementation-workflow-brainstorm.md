# Brainstorm: Agentic Feature Implementation Workflow

**Date:** 2026-03-05
**Status:** Draft
**Author:** Claude Code (brainstorm session)

---

## What We're Building

A single skill (`/feature`) that orchestrates the full feature lifecycle inside Claude Code, using a phased approach with human approval gates between phases. The workflow chains existing tools — `\workflows:brainstorm` → `\workflows:plan` → `\workflows:work` — and adds a verification and PR description step, turning a vague idea into a committed, tested, documented feature with minimal manual context switching.

---

## Why We're Building It

The full pipeline from idea to PR today requires too many manual steps and context switches:
- Remembering to run brainstorm before plan
- Kicking off `\workflows:work` manually after plan approval
- Separately running build / tests / lint to verify
- Writing PR descriptions by hand

The tooling already exists — it just lacks a coordinating layer that connects the steps and enforces the phase gates.

---

## Chosen Approach: Orchestrated Skill Chain

A new `.agent/skills/feature-implementor/SKILL.md` that defines an explicit orchestration protocol:

### Phases

| Phase | Action | Gate |
|---|---|---|
| 1. Ideate | Run `\workflows:brainstorm`, produce `docs/brainstorms/YYYY-MM-DD-<feature>-brainstorm.md` | Human approves brainstorm doc |
| 2. Plan | Run `\workflows:plan` with brainstorm doc as input, produce `docs/plans/YYYY-MM-DD-<feature>-plan.md` | Human approves plan doc |
| 3. Implement | Run `\workflows:work` with approved plan; use parallel sub-agents for independent tasks (e.g., controller, tests, frontend) | Agent signals completion |
| 4. Verify | Run `dotnet build AllProjects.slnx -c Debug`, `dotnet test`, `npm run build`, `npm run lint`; surface failures | All checks green |
| 5. PR | Generate PR description from plan + git diff; include command output as required by CLAUDE.md conventions | Human reviews PR |

### Phase Gate Behavior
- Each phase must complete successfully before the next starts
- If a phase fails (e.g., tests red), the workflow stops and surfaces the failure clearly
- Human approval is explicit: the agent pauses and waits after phases 1 and 2
- On startup, the skill checks for existing brainstorm/plan docs matching the feature slug (`YYYY-MM-DD-<slug>`) and offers to skip completed phases (resume behavior)

---

## Where It Lives

`.agent/skills/feature-implementor/SKILL.md` — fits existing conventions exactly. The skill directory is named `feature-implementor` but the Claude Code slash command is `/feature`. Invoked via:

```
/feature "add reverb effect to chord voicing preview"
```

Or without a description to start the brainstorm dialogue interactively.

---

## Key Decisions

1. **Skill, not code** — Implementation is a `SKILL.md` file with orchestration instructions, not C# or a new CLI command. Zero build cost; immediately usable.
2. **Phase gates are mandatory** — Human approves brainstorm and plan before any code is written. Prevents wasted implementation work.
3. **Reuse existing workflows** — `\workflows:brainstorm`, `\workflows:plan`, `\workflows:work` are called as sub-steps, not reimplemented.
4. **Verification is built-in** — The skill explicitly runs build + test + lint before declaring success; no "it works on my machine" shortcuts.
5. **PR description is generated** — Follows CLAUDE.md PR conventions (impact summary, linked issues, command output, UI captures for frontend changes).
6. **Parallel sub-agents where applicable** — During the Implement phase, independent work streams (backend controller, tests, frontend component) can be dispatched as parallel sub-agents via `\workflows:work`.

---

## Scope Boundaries (YAGNI)

- **In scope:** Orchestration skill file + phase gate protocol
- **Out of scope (for now):** GaCLI command wrapper, MCP server exposure, cross-session backlog tracking, autonomous PR creation without human review
- **Future consideration:** If parallel multi-agent work on large features becomes a bottleneck, extend with the Approach 2 backlog protocol

---

## Resolved Questions

1. **Slash command name:** `/feature` — short and intuitive.
2. **Partial feature restart:** Skill auto-detects existing brainstorm/plan docs matching the feature slug and offers to skip completed phases.
3. **Naming convention:** Enforced — brainstorm doc, plan doc, and git branch all share the same `YYYY-MM-DD-<slug>` identifier (e.g., branch `feat/2026-03-05-reverb-voicing-preview`, plan `docs/plans/2026-03-05-reverb-voicing-preview-plan.md`).

---

## Success Criteria

- A developer can go from a one-line feature description to a reviewable PR by approving two documents (brainstorm, plan) and running one command
- Build, tests, and lint are always verified before the PR step
- The skill is usable today without any code changes to the repository
