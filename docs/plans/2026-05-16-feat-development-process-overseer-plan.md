---
title: "feat: development process overseer"
type: feat
status: active
date: 2026-05-16
owner: GA automation
related:
  - docs/automation/chatbot-loop.md
  - docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md
  - Scripts/project-sync.ps1
  - Scripts/loop-killswitch.ps1
---

# Development Process Overseer

## Problem

GA now has autonomous coding primitives: `/auto-optimize`, `/chatbot-iterate`,
loop kill switches, Demerzel status emission, tribunal gates, and Claude Code
`/goal`. The missing layer is an independent process overseer that answers:

- Is the current AI development cycle allowed to continue?
- Is the repo in a state where `/goal`, `/loop`, or a Stop hook is the right
  mechanism?
- Is Claude drifting outside the declared scope boundary?
- Is the oracle trustworthy enough to let an optimization loop edit code?
- What workflow change should the agent apply next?

The current `chatbot-qa` cycle exposed the gap: Claude was active, had a lock,
and had rebased correctly, but the oracle output was still contract-invalid and
the worktree had dirty files outside the loop's allow-list. That should be a
deterministic pause recommendation, not a human memory task.

## Design

Use a deterministic script as the repo-local control plane:

```powershell
pwsh Scripts/dev-process-overseer.ps1 -Domain chatbot-qa
pwsh Scripts/dev-process-overseer.ps1 -Json
```

The script reads:

- `state/.loop-halted`
- `state/quality/<domain>/baseline.json`
- `state/quality/<domain>/.lock`
- `state/quality/<domain>/.STOP`
- `state/quality/<domain>/last.json`
- `state/quality/<domain>/loop-history.jsonl`
- `git status --porcelain`

It emits:

- `workflowMode`: `pause`, `supervised-goal`, or `loop-eligible`
- blocking findings and warnings
- dirty files outside the active loop scope
- protected path touches
- recommended Claude `/goal` and `/loop` prompts

## Claude Code Mapping

Use each Claude automation primitive for the job it is good at:

| Primitive | Use when | Overseer role |
|---|---|---|
| `/goal` | A substantial task has a verifiable end state. | Recommend a bounded condition and require Claude to surface command outputs, because the goal evaluator reads the transcript and does not run commands independently. |
| `/loop` | Work should recur on an interval. | Require each loop body to run the overseer first and continue only when `workflowMode=loop-eligible`. |
| Stop hook | Every session should obey a deterministic rule. | Later phase: wire the overseer as a Stop hook so unsafe states are surfaced automatically after each turn. |
| Kill switch | Any autonomous work should halt immediately. | Existing `state/.loop-halted` and per-domain `.STOP` remain the hard stop levers. |

## MVP Shipped

- `Scripts/dev-process-overseer.ps1`
- Domain-scoped scan via `-Domain`
- Machine-readable output via `-Json`
- Scope-boundary check against `baseline.json` `allow_edit`
- Protected-path check against `baseline.json` `protected_paths`
- Oracle-shape validation for `metric_value` and `oracle_status`
- Stale/recent-abort warnings
- Claude `/goal` and `/loop` recommendation templates

## Current Finding: `chatbot-qa`

Running the overseer on 2026-05-16 returned:

```text
Mode: pause
Blocks: 2
Warnings: 1
```

Blocking reasons:

- `last.json` is missing `metric_value` and `oracle_status`
- dirty files exist outside the chatbot QA `allow_edit` scope

Warning:

- the recent loop history includes `aborted-oracle-unreliable`

Decision: do not allow the current optimization loop to commit until one clean
supervised oracle run emits the required shape and unrelated worktree changes
are isolated.

## Next Phases

### Phase 1: Hardening

- Add `docs/schemas/dev-process-overseer.schema.json`
- Add Pester tests for:
  - missing oracle output
  - invalid oracle shape
  - out-of-scope dirty files
  - protected-path dirty files
  - active kill switch
- Add `-FailOnBlock` so CI and hooks can treat blocks as non-zero exit.

### Phase 2: Claude Integration

- Add a Claude Code Stop hook that runs:

  ```powershell
  pwsh Scripts/dev-process-overseer.ps1 -Json
  ```

- The hook should surface a concise summary, not auto-kill the process.
- Update `.claude/skills/auto-optimize/SKILL.md` to require the overseer before
  every commit attempt.
- Add a canonical `/goal` template to the auto-optimize skill.

### Phase 3: Cross-Repo Agent Blackbox

- Copy the script shape to `ix`, `Demerzel`, and `tars`, with repo-specific
  baseline fields.
- Emit a normalized artifact:

  ```text
  state/governance/dev-process-overseer.json
  ```

- Let Demerzel or Agent Blackbox aggregate all sibling repo overseer artifacts
  and recommend cross-repo workflow changes.

### Phase 4: Recommendations Engine

Convert raw findings into workflow recommendations:

- "Use `/goal`" when the task is bounded and verifiable.
- "Use `/loop`" when a green state should be maintained over time.
- "Use a Stop hook" when the rule should apply to every session.
- "Create a baseline" when no loop contract exists.
- "Add a roundtrip validator" when the loop can improve a metric while breaking
  reversibility.
- "Escalate to tribunal" when protected paths or AI/ML orchestration code move.

## Acceptance Criteria

- The overseer blocks the exact unsafe state observed in the current
  `chatbot-qa` cycle.
- A Claude session can paste the recommended `/goal` and get a bounded,
  verifiable completion condition.
- A `/loop` body can run the overseer first and refuse to continue on `pause`.
- The JSON output is schema-validated.
- Demerzel can consume the artifact without knowing GA internals.
