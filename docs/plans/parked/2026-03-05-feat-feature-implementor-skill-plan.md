---
title: "feat: /feature Skill — Agentic Feature Implementation Orchestrator"
type: feat
status: completed
date: 2026-03-05
origin: docs/brainstorms/2026-03-05-agentic-feature-implementation-workflow-brainstorm.md
---

# feat: /feature Skill — Agentic Feature Implementation Orchestrator

## Overview

Create `.agent/skills/feature-implementor/SKILL.md`, a Claude Code skill that orchestrates the full feature lifecycle — brainstorm → plan → implement → verify → PR — with mandatory human approval gates between phases. The skill chains existing `\workflows:` sub-commands and adds coordination logic (slug convention, resume detection, branch management, verification, PR generation) that today requires manual effort and context switching.

The skill is invoked as `/feature "add reverb to voicing preview"` and guides the developer through each phase, pausing for human approval at critical gates.

(see brainstorm: docs/brainstorms/2026-03-05-agentic-feature-implementation-workflow-brainstorm.md)

---

## Problem Statement

The current idea-to-PR pipeline has friction at every stage:
- Running `\workflows:brainstorm` then manually passing output to `\workflows:plan` then `\workflows:work`
- No enforcement of naming conventions across brainstorm doc, plan doc, and git branch
- No built-in verification before claiming success
- PR descriptions written by hand every time

The tooling already exists — it just lacks a coordinating layer that enforces the protocol.

---

## Proposed Solution

A single `SKILL.md` file with orchestration instructions that the Claude Code agent follows. No C# code, no CLI commands, zero build cost.

The skill defines:
1. A 5-phase pipeline with two human approval gates
2. A slug convention shared by all artifacts
3. A 4-state resume machine
4. Git branch management rules
5. Scope-aware verification
6. PR description generation per CLAUDE.md conventions

---

## Deliverables

### 1. `.agent/skills/feature-implementor/SKILL.md`

The skill file. Full specification below.

**Frontmatter:**
```yaml
---
name: feature-implementor
description: "Orchestrate the full feature lifecycle — brainstorm → plan → implement → verify → PR — with mandatory human approval gates and consistent artifact naming."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, Agent, AskUserQuestion, Skill
---
```

**Structural sections** (follow project-authored Archetype A pattern from existing skills):

#### Overview (tagline)
> Connects `\workflows:brainstorm` → `\workflows:plan` → `\workflows:work` into a single, gated pipeline. Enforces consistent artifact naming and runs verification before every PR.

#### Core Principle
> A feature is not done until build, tests, and lint are green. No exceptions.

#### Iron Law (fenced block)
```
NEVER proceed to the next phase without explicit human approval at each gate.
NEVER claim implementation complete without running Phase 4 verification.
NEVER create a PR on a branch with failing tests or a red build.
```

#### When to Use / Do Not Use

**Use for:**
- Any new feature, fix, or refactor requiring design decisions
- Work that spans multiple files, layers, or services
- Anything that should result in a PR

**Do not use for:**
- Single-file edits or hotfixes (use `\workflows:work` directly)
- Experimental/throwaway code
- Already-in-progress work without existing docs (use resume flow instead)

#### Invocation

```
/feature "<description>"       # Start new feature from description
/feature                       # Start interactively (skill asks for description)
```

#### Slug Derivation Algorithm

Before any other action, derive the `SLUG` from the feature description:
1. Lowercase the description
2. Replace spaces with hyphens
3. Strip all characters that are not alphanumeric or hyphens
4. Truncate to 40 characters; do not cut in the middle of a word
5. Prepend today's date: `YYYY-MM-DD-<slug>`

Examples:
- `"Add reverb to chord voicing preview"` → `2026-03-05-add-reverb-to-chord-voicing-preview`
- `"Fix: NullRef in TabAwareOrchestrator"` → `2026-03-05-fix-nullref-in-tabawareorchestrator`

The `SLUG` is shared by: brainstorm doc path, plan doc path, git branch name. Never deviate.

#### Dirty Working Tree Guard

Before Phase 1 (or resume), check for uncommitted changes:
```bash
git status --porcelain
```
If any output exists, STOP and instruct the developer to commit, stash, or discard changes before invoking `/feature`.

#### Resume State Machine

On startup, check for existing artifacts to detect resume state:

| State | Condition | Action |
|---|---|---|
| `fresh` | No docs matching SLUG | Start Phase 1 |
| `brainstorm-only` | `docs/brainstorms/<SLUG>-brainstorm.md` exists, no plan | Ask "Resume from Phase 2 (Plan)?" — if yes, re-read brainstorm doc to prime context before calling `\workflows:plan` |
| `plan-only` | Both brainstorm and plan docs exist, no branch commits | Ask "Resume from Phase 3 (Implement)?" — if yes, re-read both docs before calling `\workflows:work` |
| `in-progress` | Branch `feat/<SLUG>` exists with commits | Ask "Resume from Phase 4 (Verify)?" — if yes, run verification directly |

When resuming, always re-read the existing artifact(s) to restore context before delegating to the next sub-workflow.

#### Phase 1: Ideate

Delegate to `\workflows:brainstorm` with the feature description as input.

Expected output: `docs/brainstorms/<SLUG>-brainstorm.md`

**Approval gate:** Present the doc path and ask:
> "Phase 1 complete. Brainstorm written to `docs/brainstorms/<SLUG>-brainstorm.md`.
> **Approve** to proceed to planning, **Edit** to revise the doc first, or **Abort** to stop."

Continue only on "Approve". On "Edit", wait for the developer to edit and re-present the same gate. On "Abort", stop and leave the brainstorm doc in place for later resumption.

#### Phase 2: Plan

Delegate to `\workflows:plan` with the brainstorm doc path as input.

Expected output: `docs/plans/<SLUG>-plan.md`

**Approval gate:** Same pattern as Phase 1:
> "Phase 2 complete. Plan written to `docs/plans/<SLUG>-plan.md`.
> **Approve** to begin implementation, **Edit** to revise, or **Abort** to stop."

#### Phase 2→3 Transition: Branch Setup

Before Phase 3, create the feature branch:
```bash
git checkout main
git pull origin main
git checkout -b feat/<SLUG>
```

If `feat/<SLUG>` already exists (resume path), check it out without recreating it:
```bash
git checkout feat/<SLUG>
```

If neither `main` nor `feat/<SLUG>` exists as expected, surface the error and stop.

#### Phase 3: Implement

Delegate to `\workflows:work` with the approved plan doc path as input.

For plans containing clearly independent task groups (e.g., a "backend controller" task and a "frontend component" task with no shared files), dispatch parallel sub-agents. Task independence criteria:
- Tasks write to entirely different file paths (no overlap)
- Tasks have no declared dependency on each other in the plan
- Each task can be verified in isolation

If parallel sub-agents are used:
- Collect all results before proceeding
- If any sub-agent reports failure, surface ALL failures together before Phase 4
- Do not proceed to Phase 4 until all failures are resolved

**Sub-agent failure policy:** Never silently continue. List each failing task with its error output. Present the developer with: "N sub-agent(s) failed. Resolve the above failures, then re-run Phase 3 or proceed to verification."

#### Phase 4: Verify

Run verification in this order. Stop and report on first failure unless noted.

**Step 4.0 — Format (auto-fix):**
```powershell
dotnet format AllProjects.slnx
```
Run unconditionally. Auto-fixes formatting so the pre-commit hook does not fail in Phase 5.

**Step 4.1 — Build:**
```powershell
dotnet build AllProjects.slnx -c Debug
```

**Step 4.2 — Tests:**
```powershell
dotnet test AllProjects.slnx
```

**Steps 4.3 & 4.4 — Frontend (scope-gated):**

Check if any frontend files were modified:
```bash
git diff main...HEAD --name-only | grep -E "^(Apps/ga-client|ReactComponents)/."
```
Only run the following if output is non-empty:
```bash
# In ReactComponents/ga-react-components (shared component library)
cd ReactComponents/ga-react-components && npm run build && npm run lint

# In Apps/ga-client (main app, if its files changed)
cd Apps/ga-client && npm run build && npm run lint
```

**Verification failure handling:**
- Surface the exact command output (stdout + stderr)
- State which step failed
- Do not proceed to Phase 5
- After the developer fixes the failure, they may re-invoke `/feature` — the resume machine will detect the `plan-only` or `in-progress` state and skip to Phase 4

#### Phase 5: PR

**Push branch:**
```bash
git push -u origin feat/<SLUG>
```

**Generate PR description** following CLAUDE.md conventions. The PR body must include:

```markdown
## Summary
[2-3 sentence impact summary derived from the plan's Overview section]

## Changes
[Bullet list of key changes, derived from the plan's Implementation Phases or Acceptance Criteria]

## Verification
[Paste actual output from Phase 4 steps 4.1 and 4.2]

## Linked Issues
[If a GitHub issue number was mentioned anywhere in the brainstorm or plan, format as `Fixes #NNN`. Otherwise, omit this section — do not leave a placeholder with #NNN.]

## UI Changes
[If frontend files were modified: insert placeholder text: "**Screenshots required** — attach before/after captures of affected UI."]
[If backend-only: omit this section.]
```

**Create PR:**
```bash
gh pr create \
  --base main \
  --head feat/<SLUG> \
  --title "feat: <SLUG without date prefix>" \
  --body-file /tmp/pr-body.md
```

If `gh` is not available, output the PR body and instruct the developer to create the PR manually.

#### Anti-Patterns

| Anti-pattern | Correct behavior |
|---|---|
| Proceeding past a gate without explicit "Approve" | Always wait for the approval word; treat ambiguous responses as "Edit" |
| Implementing before the plan is approved | Phase gate blocks this |
| Skipping Phase 4 when "the tests probably pass" | Iron Law — always verify |
| Running `npm` commands for a backend-only change | Scope gate skips them |
| Deriving a different slug on resume than on initial run | Slug is always derived from the original description; if ambiguous, show the derivation and ask for confirmation |
| Claiming Phase 3 done when a sub-agent failed | Collect all failures, surface together, halt |

#### File Paths Reference

| Artifact | Path |
|---|---|
| Brainstorm doc | `docs/brainstorms/<SLUG>-brainstorm.md` |
| Plan doc | `docs/plans/<SLUG>-plan.md` |
| Git branch | `feat/<SLUG>` |
| Verification commands | See CLAUDE.md `## Commands > Verification` |
| Build command | `dotnet build AllProjects.slnx -c Debug` |
| Test command | `dotnet test AllProjects.slnx` |
| Format command | `dotnet format AllProjects.slnx` |
| Frontend (shared lib) | `ReactComponents/ga-react-components/` |
| Frontend (main app) | `Apps/ga-client/` |

---

### 2. CLAUDE.md Skills Table Update

Add one row to the `## Agent Skills` table in `CLAUDE.md`:

```markdown
| Feature Implementor | `.agent/skills/feature-implementor/SKILL.md` | Running `/feature` for any new feature, fix, or multi-file refactor |
```

---

## Technical Considerations

- The skill is pure markdown — no compilation, no dependencies, zero risk of breaking the build
- `\workflows:brainstorm`, `\workflows:plan`, and `\workflows:work` are compound-engineering sub-commands invoked via the `Skill` tool; they are already installed in this project
- The `gh` CLI is used for PR creation; the skill must gracefully degrade if it is absent
- The `git` and `dotnet` commands are already part of the development environment (CLAUDE.md confirms their availability)
- The `npm` scope-gate uses `git diff main...HEAD` — this correctly handles the case where the feature branch diverged from `main` multiple commits ago

---

## System-Wide Impact

- **CLAUDE.md**: One new row in the Agent Skills table. No other changes.
- **No code changes**: The skill is a markdown file; it changes no runtime behaviour.
- **Existing workflows unaffected**: `\workflows:brainstorm`, `\workflows:plan`, `\workflows:work` are called by the skill but not modified.
- **Pre-commit hook**: Phase 4 runs `dotnet format` before verification, so the hook (which runs `dotnet format --verify-no-changes`) will not fail in Phase 5.

---

## Acceptance Criteria

- [x] `.agent/skills/feature-implementor/SKILL.md` exists and follows Archetype A structure (project-authored skill pattern)
- [x] Frontmatter includes `name`, `description`, and `allowed-tools`; no `risk` or `source` fields
- [x] Iron Law block is present and non-negotiable
- [x] Slug derivation algorithm is documented with examples
- [x] Dirty working tree guard is specified
- [x] All four resume states (`fresh`, `brainstorm-only`, `plan-only`, `in-progress`) are explicitly defined
- [x] Approval gate at Phase 1 and Phase 2 uses the Approve / Edit / Abort prompt pattern
- [x] Phase 2→3 branch creation is specified (creates `feat/<SLUG>` from `main`)
- [x] Phase 3 parallel sub-agent coordination rules are documented (independence criteria, failure collection policy)
- [x] Phase 4 runs `dotnet format` (auto-fix) as step 0 before build
- [x] Phase 4 frontend scope gate skips npm commands for backend-only features
- [x] Phase 5 PR body follows CLAUDE.md conventions; UI captures section is conditional
- [x] Phase 5 degrades gracefully if `gh` CLI is absent
- [x] Anti-patterns table covers the 6 listed failure modes
- [x] CLAUDE.md `## Agent Skills` table updated with `feature-implementor` entry
- [x] Skill is invocable as `/feature` immediately after creation, with no code changes to the repository

---

## Dependencies & Risks

| Item | Notes |
|---|---|
| `\workflows:brainstorm`, `\workflows:plan`, `\workflows:work` | Required sub-skills; confirmed installed in this project via compound-engineering plugin |
| `gh` CLI | Used for PR creation; skill must degrade if absent |
| `dotnet` SDK | Required for Phase 4; available in dev environment per CLAUDE.md |
| Node/npm | Required for Phase 4 frontend scope; available in dev environment |
| `git` | Required throughout; always available |

**Risk:** If the slug of a resumed feature is derived differently from the original (e.g., different date), resume detection silently finds no match. Mitigation: the skill shows the derived slug before taking any action and asks for confirmation if it is ambiguous.

---

## Sources & References

### Origin
- **Brainstorm document:** [docs/brainstorms/2026-03-05-agentic-feature-implementation-workflow-brainstorm.md](docs/brainstorms/2026-03-05-agentic-feature-implementation-workflow-brainstorm.md)
  - Key decisions carried forward: (1) skill-not-code approach, (2) 5-phase pipeline with gates after phases 1 and 2, (3) slug-based artifact naming enforced, (4) resume auto-detect, (5) reuse existing `\workflows:` sub-commands

### Internal References
- Skill structure pattern: `.agent/skills/verification-before-completion/SKILL.md` (Iron Law + GA commands archetype)
- Skill structure pattern: `.agent/skills/systematic-debugging/SKILL.md` (four-phase procedural)
- Multi-agent coordination: `docs/architecture/AGENT_TEAM_OPERATING_PLAYBOOK.md`
- Verification commands: `CLAUDE.md` § Commands > Verification
- Pre-commit hook behavior: `CLAUDE.md` § Commit Conventions
- Agent skills registration convention: `CLAUDE.md` § Agent Skills table
