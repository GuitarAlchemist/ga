---
name: feature-implementor
description: "Orchestrate the full feature lifecycle — brainstorm → plan → implement → verify → PR — with mandatory human approval gates and consistent artifact naming."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, Agent, AskUserQuestion, Skill
---

# Feature Implementor

Connects `\workflows:brainstorm` → `\workflows:plan` → `\workflows:work` into a single, gated pipeline. Enforces consistent artifact naming and runs verification before every PR.

**Core principle:** A feature is not done until build, tests, and lint are green. No exceptions.

## The Iron Law

```
NEVER proceed to the next phase without explicit human approval at each gate.
NEVER claim implementation complete without running Phase 4 verification.
NEVER create a PR on a branch with failing tests or a red build.
```

## When to Use

- Any new feature, fix, or refactor requiring design decisions
- Work that spans multiple files, layers, or services
- Anything that should result in a PR

## Do Not Use When

- Single-file edits or hotfixes (use `\workflows:work` directly with the file path)
- Experimental or throwaway code
- Already in-progress work without existing docs (use resume flow instead)

## Invocation

```
/feature "<description>"    # Start from description
/feature                    # Interactive mode — skill asks for description
```

---

## Step 0: Slug Derivation

Before any other action, derive the `SLUG` from the feature description:

1. Lowercase the description
2. Replace spaces with hyphens
3. Strip all characters that are not alphanumeric or hyphens
4. Truncate to 40 characters; do not cut mid-word
5. Prepend today's date: `YYYY-MM-DD-<slug>`

Examples:
- `"Add reverb to chord voicing preview"` → `2026-03-05-add-reverb-to-chord-voicing-preview`
- `"Fix: NullRef in TabAwareOrchestrator"` → `2026-03-05-fix-nullref-in-tabawareorchestrator`

Show the derived slug to the developer before proceeding. Ask for confirmation if the derivation is ambiguous.

The `SLUG` is shared by: brainstorm doc path, plan doc path, git branch name. Never deviate.

---

## Step 1: Dirty Working Tree Guard

```bash
git status --porcelain
```

If any output exists, **stop** and instruct the developer to commit, stash, or discard changes before invoking `/feature`. Do not proceed.

---

## Step 2: Resume Detection

Check for existing artifacts to detect resume state:

| State | Condition | Action |
|---|---|---|
| `fresh` | No docs matching SLUG | Start Phase 1 |
| `brainstorm-only` | `docs/brainstorms/<SLUG>-brainstorm.md` exists, no plan doc | Ask "Resume from Phase 2 (Plan)?" — if yes, re-read brainstorm doc before calling `\workflows:plan` |
| `plan-only` | Both brainstorm and plan docs exist, no branch commits | Ask "Resume from Phase 3 (Implement)?" — if yes, re-read both docs before calling `\workflows:work` |
| `in-progress` | Branch `feat/<SLUG>` exists with commits | Ask "Resume from Phase 4 (Verify)?" — if yes, run Phase 4 directly |

When resuming, always re-read existing artifacts to restore context before delegating to the next sub-workflow.

---

## Phase 1: Ideate

Delegate to `\workflows:brainstorm` with the feature description as input.

**Expected output:** `docs/brainstorms/<SLUG>-brainstorm.md`

**Approval gate:**
> "Phase 1 complete. Brainstorm written to `docs/brainstorms/<SLUG>-brainstorm.md`.
> **Approve** to proceed to planning, **Edit** to revise the doc first, or **Abort** to stop."

- **Approve** → continue to Phase 2
- **Edit** → wait for developer to edit; re-present the same gate
- **Abort** → stop; leave the brainstorm doc in place for resumption later

---

## Phase 2: Plan

Delegate to `\workflows:plan` with the brainstorm doc path as input.

**Expected output:** `docs/plans/<SLUG>-plan.md`

**Approval gate:**
> "Phase 2 complete. Plan written to `docs/plans/<SLUG>-plan.md`.
> **Approve** to begin implementation, **Edit** to revise, or **Abort** to stop."

Same Approve / Edit / Abort semantics as Phase 1.

---

## Phase 2 → 3 Transition: Branch Setup

Before Phase 3, set up the feature branch:

```bash
# New feature (fresh state)
git checkout main
git pull origin main
git checkout -b feat/<SLUG>

# Resume (branch already exists)
git checkout feat/<SLUG>
```

If neither `main` nor `feat/<SLUG>` resolves correctly, surface the error and stop.

---

## Phase 3: Implement

Delegate to `\workflows:work` with the approved plan doc path as input.

**Parallel sub-agents:** For plans containing clearly independent task groups, dispatch parallel sub-agents. Independence criteria:
- Tasks write to entirely different file paths (no overlap)
- Tasks have no declared dependency on each other in the plan
- Each task can be verified in isolation

**Sub-agent failure policy:** Collect all results before proceeding. If any sub-agent reports failure, surface **all** failures together. Do not proceed to Phase 4 until all failures are resolved. Never silently continue past a failed sub-agent.

---

## Phase 4: Verify

Run in this exact order. Stop and report on first failure (except step 4.0 which auto-fixes).

**Step 4.0 — Format (auto-fix, always run):**
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

First, check if any frontend files were modified:
```bash
git diff main...HEAD --name-only | grep -E "^(Apps/ga-client|ReactComponents)/."
```

Only run if output is non-empty:
```bash
# Shared component library
cd ReactComponents/ga-react-components && npm run build && npm run lint

# Main app (only if its files changed)
cd Apps/ga-client && npm run build && npm run lint
```

**On verification failure:**
- Surface the exact command output (stdout + stderr)
- State which step failed
- Do not proceed to Phase 5
- After the developer fixes the failure, re-invoke `/feature` — the resume machine detects `plan-only` or `in-progress` state and skips to Phase 4

---

## Phase 5: PR

**Push branch:**
```bash
git push -u origin feat/<SLUG>
```

**Generate PR description** following CLAUDE.md conventions:

```markdown
## Summary
[2-3 sentence impact summary from the plan's Overview]

## Changes
[Bullet list of key changes from the plan's Acceptance Criteria or Implementation Phases]

## Verification
[Paste actual output from Phase 4 steps 4.1 and 4.2]

## Linked Issues
[Only include if a GitHub issue number appears in brainstorm or plan: "Fixes #NNN"
 Omit entirely if no issue number was mentioned — do not leave a placeholder]

## UI Changes
[If frontend files were modified: "**Screenshots required** — attach before/after captures of affected UI."]
[If backend-only: omit this section]
```

**Create PR:**
```bash
gh pr create \
  --base main \
  --head feat/<SLUG> \
  --title "feat: <human-readable description>" \
  --body-file /tmp/pr-body.md
```

If `gh` is not available, output the PR body and instruct the developer to create the PR manually via the GitHub web UI.

---

## Anti-Patterns

| Anti-pattern | Correct behavior |
|---|---|
| Proceeding past a gate without explicit "Approve" | Always wait for the approval word; treat ambiguous responses as "Edit" |
| Implementing before the plan is approved | Phase 2 gate blocks this |
| Skipping Phase 4 because "tests probably pass" | Iron Law — always run verification |
| Running `npm` commands for a backend-only change | Scope gate skips them automatically |
| Deriving a different slug on resume than initial run | Slug is always derived from the original description; show derivation and ask for confirmation if ambiguous |
| Claiming Phase 3 done when a sub-agent failed | Collect all failures, surface together, halt before Phase 4 |

---

## File Paths Reference

| Artifact | Path |
|---|---|
| Brainstorm doc | `docs/brainstorms/<SLUG>-brainstorm.md` |
| Plan doc | `docs/plans/<SLUG>-plan.md` |
| Git branch | `feat/<SLUG>` |
| Build command | `dotnet build AllProjects.slnx -c Debug` |
| Test command | `dotnet test AllProjects.slnx` |
| Format command | `dotnet format AllProjects.slnx` |
| Frontend (shared lib) | `ReactComponents/ga-react-components/` |
| Frontend (main app) | `Apps/ga-client/` |
| Related workflows | `\workflows:brainstorm`, `\workflows:plan`, `\workflows:work` |
