# Chatbot development automation — the L2 loop

How Guitar Alchemist iterates on the chatbot using the Boris-Cherny-style
Claude Code workflow, scaled to our multi-repo + multi-LLM-review
ecosystem. Cross-references the 13 techniques from his "Inside Claude
Code" interview against what we already have in this repo, what we just
added, and what we deliberately don't do.

## Levels of automation

We treat chatbot automation as a four-level ladder, picking only as
much automation as the quality signal can underwrite:

| Level | Description | Status |
|-------|-------------|--------|
| L1 — Assistant-driven | Single session per item; assistant runs `/feature` or `/work`, human reviews and merges. | **In use today.** |
| L2 — Single-shot autonomous | `/chatbot-iterate` picks one backlog item, runs the pipeline end-to-end, opens the PR; human merges. | **Active — see below.** |
| L3 — Auto-merge on multi-LLM green | Same as L2 but auto-merges when octo:review and Demerzel tribunal both pass. | **Not enabled.** Waiting for tribunal stability + production canary. |
| L4 — Dark factory | Failure traces in production seed the backlog automatically; loop reads, fixes, ships without human in the loop. | **Not feasible yet.** Telemetry pipeline missing. |

## The L2 loop

```
                ┌─ /chatbot-iterate ─┐
                │                    │
   BACKLOG.md ──┤  Step 1            │
                │  pick item         │
                │                    │
                │  Step 2            │
                │  classify gates ───┼──> tribunal? octo:review? smoke?
                │                    │    (paths-based, see Iron Law)
                │                    │
                │  Step 3            │
                │  run pipeline ─────┼──> /feature → /ce-plan → /ce-work
                │                    │    → dotnet build/test
                │                    │    → npm run build/lint
                │                    │
                │  Step 4            │
                │  multi-LLM gate ───┼──> /octo:review (correctness/security)
                │                    │    Demerzel QA Architect tribunal
                │                    │    (music-theory verdict)
                │                    │
                │  Step 5            │
                │  open PR ──────────┼──> /ce-commit-push-pr with body
                │                    │    (item link + tests + reviews)
                │                    │
                │  Step 6            │
                │  post-merge ───────┼──> /learnings + /ce-compound
                │                    │
                └────────────────────┘
                          │
                          └─> human reviews and merges
```

## Iron Law: tribunal-required paths

Any PR that touches the following paths **must** clear both `/octo:review`
and the Demerzel tribunal before merge:

```
Common/GA.Business.ML/Agents/**
Common/GA.Business.ML/**/Mcp/**
Apps/ga-server/GaApi/Mcp/**
Common/GA.Business.DSL/**
**/IChatClientFactory*
**/AddGuitarAlchemistAi*
```

`Scripts/check-chatbot-tribunal-gate.ps1` inspects the changed files
versus `main` and prints which gates apply. Wire it into the Stop hook
or run manually to confirm the right gates before opening the PR.

The rule is path-based, not size-based. PR #151 evidence
(`feedback_multi_llm_review_pays_off` memory): multi-LLM review caught
**9 real bugs** that local tests missed in a chatbot migration. The gate
is load-bearing, not bureaucratic.

## Boris's 13 techniques — what we have, what's new, what we skip

Cross-referenced against the 13 from "Inside Claude Code" (Ewan Mak,
2026-01-05).

| # | Boris's technique | GA status |
|---|-------------------|-----------|
| 1 | **Run multiple Claude instances in parallel** | User-level discipline, not repo-enforced. We support it (separate worktrees via `ce-worktree`, antigravity-bridge for hand-off between AG + Claude Code). |
| 2 | **Opus thinking-mode by default** | We're on Opus 4.7 (1M context) for this session; user picks per session. |
| 3 | **CLAUDE.md as team knowledge base** | We have `CLAUDE.md` plus `.agent/skills/` *(gitignored, per-user)* and `.claude/skills/` *(committed, repo-wide)*. Errors update memory + solutions, not CLAUDE.md directly — different shape, same idea. |
| 4 | **Plan Mode by default** | Available via Shift+Tab. We use `/feature` and `compound-engineering:ce-plan` as the planning entrypoints. |
| 5 | **Slash commands for recurring tasks** | Long list under `.claude/skills/` and the compound-engineering plugin. **NEW**: `/chatbot-iterate` and `/learnings` added in this round. |
| 6 | **Sub-agents for tasks, not personas** | Our `compound-engineering:ce-*` set has both task-shaped agents (`ce-debug`, `ce-plan`, `ce-work`) and named-after-people personas (`ce-kieran-*`, `ce-dhh-*`, `ce-julik-*`). Empirically the persona ones produce useful pushback, so we keep them — but Boris's caution is noted. |
| 7 | **PostToolUse hooks for cleanup** | We have one for DSL files. Skipped `dotnet format`-on-edit deliberately because .NET autoformat is slower than the TS/Python tools Boris is optimising for. |
| 8 | **/permissions whitelist over `--dangerously-skip-permissions`** | We use `.claude/settings.local.json` allow-lists. Already there. |
| 9 | **Tool integration: Slack, BigQuery, Sentry** | Slack research via `compound-engineering:ce-slack-research`, lots of MCP tools. No Sentry/BigQuery yet. |
| 10 | **ralph-wiggum loop plugin** | We have the canonical `loop` skill from Anthropic plus `octo:loop`. Enough. |
| 11 | **Feedback loops (2-3× quality)** | Tests + multi-LLM review + Demerzel tribunal (`Demerzel/pipelines/qa-architect-cycle.ixql`, **not** `qa-tribunal.ixql`) = three feedback signals. Empirically load-bearing per PR #151. |
| 12 | **Stop hook surfaces session state** | **NEW**: `Scripts/session-pr-check.ps1` runs on `Stop` to surface PR + CI status. Closes the "shipped on local green, red on CI" gap. |
| 13 | **`@.claude` tag in code review for auto-update** | Not implemented. Possibly a `/claude-md-correction` skill in the future — for now corrections go into `docs/solutions/` via `/learnings`. |

## What we deliberately do *not* do

- **Auto-merge on green octopus alone**. Tribunal verdict is a separate
  signal (music-theory correctness, cross-LLM consensus on contested
  decisions) that catches different things. Both gates exist for
  different reasons.
- **`dotnet format` PostToolUse hook**. Pre-commit hook covers it; the
  per-edit cost is too high for .NET.
- **Duplicate slash command surfaces** (`/scope`, `/critique`, `/work`
  outside `\workflows:*`). The compound-engineering plugin already has
  these; second name set dilutes muscle memory.
- **Single-repo Boris patterns**. GA shares JSON-on-disk contracts with
  ix / Demerzel / tars. Some Boris workflows assume one repo + one
  developer; we layer Demerzel governance for cross-repo coordination.

## Scheduling L2

For a pure `/chatbot-iterate` cadence:

```sh
# Once-per-weekday autonomous iteration (Mon-Fri, 09:00 local).
# Uses the schedule skill from Anthropic.
/schedule add chatbot-iterate "0 9 * * 1-5" --prompt "/chatbot-iterate"
```

Or via the compound-engineering loop:

```sh
/loop 1d "/chatbot-iterate"
```

Stop conditions for the loop (in priority order):

1. The skill reports "no work to do" (backlog exhausted or all blocked).
2. A gate fails (test red, octopus blocks, tribunal vetoes) — drops out
   for human attention.
3. Three consecutive iterations produce no new merged PR — likely
   stuck on a blocker, surfaces for review.
4. User says stop.

## Promoting to L3

When ready to consider auto-merge (L3), the prerequisites are:

- [ ] At least 5 chatbot PRs shipped via `/chatbot-iterate` with
      tribunal verdicts agreeing with octo:review (no false-positive
      drift).
- [ ] Production canary deployment for the chatbot path so a regression
      rolls back automatically.
- [ ] Telemetry pipeline that feeds chatbot failure traces into the
      backlog for the next loop iteration (this is the L4 prerequisite
      anyway).

Until those are checked, L2 is the ceiling.

## Related

- `.claude/skills/chatbot-iterate/SKILL.md` — the loop skill itself
- `.claude/skills/learnings/SKILL.md` — post-merge learning capture
- `Scripts/session-pr-check.ps1` — Stop-hook PR/CI summary
- `Scripts/check-chatbot-tribunal-gate.ps1` — path-based gate classifier
- `BACKLOG.md` — chatbot items live here under "chatbot" / "TheoryAgent"
  / "MCP" / "intent routing" headings
- `docs/solutions/` — accumulated learnings from past iterations
- `feedback_multi_llm_review_pays_off` (project memory) — the empirical
  argument for the gate
