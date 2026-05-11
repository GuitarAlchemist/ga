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

### L3 promotion checklist — `auto-merge-eligible` label becomes default

The mechanism is in place (`Scripts/octo-auto-merge-decision.ps1`,
SKILL.md Step 7, opt-in label `auto-merge-eligible`). What's missing
is the *evidence* that auto-merge is safe to make default. Each item
below is concrete and testable.

- [ ] **Mechanism shipped** — `octo-auto-merge-decision.ps1` exists,
      refuses by default, exits 0 only when all gates pass.
      **Status:** ✅ shipped 2026-05-10 (this session).
- [ ] **5 clean auto-merges** — at least 5 chatbot PRs shipped via the
      mechanism with zero post-merge rollbacks or hotfixes in the
      following 7 days. Measured by querying `gh pr list --search
      "is:merged label:auto-merge-eligible"` and cross-referencing
      against the same period's `git log --grep="(revert|hotfix)"`.
      **Status:** 0 / 5.
- [ ] **Production canary auto-rollback verified** — deploy intentional
      bad change to canary, confirm rollback fires within 5 min via
      whatever monitoring you wire up. Without this, "auto-merge" means
      "auto-ship a regression". Acceptance: one end-to-end rollback
      drill captured in `docs/solutions/`.
      **Status:** ❌ not started — no canary infra exists yet.
- [x] **Gate-comparison ledger mechanism** — `state/quality/gate-ledger.jsonl`
      with `Scripts/gate-ledger-write.ps1` writer + `docs/schemas/gate-ledger.schema.json`
      shipped 2026-05-10. `/chatbot-iterate` Step 5 now appends a row
      per merge. **Status:** mechanism ✅ shipped; **0/10 entries** yet —
      evidence accumulates as PRs flow.
- [ ] **CI env flakiness resolved** — `Backend Tests` / `build` /
      `Playwright Tests` no longer fail on missing Anthropic API key
      on CI runners. Either set the secret, or skip tests when env is
      missing, or split into "needs-llm" lane. Current allowlist in
      `octo-auto-merge-decision.ps1` (`-AllowlistedCiFailures`) is a
      bandage.
      **Status:** ❌ allowlist active; tests fail on every PR.
- [x] **Cost-per-auto-merge measurement** — `Scripts/octo-cost-tally.ps1`
      shipped 2026-05-10. Reads `~/.claude-octopus/metrics-session.json`,
      lets the caller pass an Agent-tool cost estimate, writes
      `state/quality/cost-ledger.jsonl`. Exits 2 if cumulative cost
      exceeds the budget. Wired into `/chatbot-iterate` Step 5 post-merge.
      **Status:** measurement ✅ shipped; setting the actual monthly
      cap is a policy decision the operator makes.

Until ALL six are checked, L2-with-explicit-label is the ceiling.

### L4 promotion checklist — failure-fed dark factory

L4 means production failures auto-create BACKLOG items the loop picks
up, with no human in the loop except for the kill-switch.
**All L3 items above are L4 prerequisites.** Additional L4-only items:

- [ ] **Production telemetry pipeline** — chatbot user errors / 4xx /
      5xx / hallucinations get logged to a structured destination the
      loop can read. Acceptance: a single `state/telemetry/failures.jsonl`
      file (or equivalent) with at least 100 real failure records.
      **Status:** ❌ chatbot has no production telemetry destination
      beyond Vercel/Cloudflare access logs.
- [ ] **Failure → BACKLOG triage skill** — new skill that reads
      `failures.jsonl`, clusters similar failures, deduplicates against
      existing BACKLOG items, files new ones with reproduction steps.
      Acceptance: ≥10 BACKLOG items filed automatically with no human
      edits required to be actionable.
      **Status:** ❌ not designed.
- [ ] **Continuous loop scheduler** — `/loop 1h "/chatbot-iterate"` runs
      reliably for 24h without blowing budget or getting stuck. Need a
      stop-condition that doesn't depend on a human terminal.
      Acceptance: 24h soak test logged in `docs/solutions/`.
      **Status:** ❌ skill exists, scheduler does not.
- [ ] **Anomaly detection on the loop's own output** — if the loop
      starts producing reverted PRs / failed merges / runaway costs,
      a separate watcher pages a human. Loop must be observable from
      OUTSIDE the loop. Acceptance: a synthetic "loop is broken" alert
      that fires within 1h.
      **Status:** ❌ no watcher.
- [x] **Always-on kill switch** — `Scripts/loop-killswitch.ps1`
      shipped 2026-05-10. Writes `state/.loop-halted` sentinel that
      `/chatbot-iterate` Step 0 checks before every iteration; refuses
      to start when present. `-Force -Yes` flag terminates running
      loop processes. `-Reset` removes the sentinel.
      **Status:** ✅ shipped + smoke-tested.

Until ALL of L3 AND L4 are checked, L4 is aspirational, not operational.

## Where this session left things

After the 2026-05-10 session that authored this document:

| Item | Status |
|---|---|
| `/learnings` skill | ✅ shipped |
| `/chatbot-iterate` skill | ✅ shipped + canary PR #155 + drive-by SemanticIntentRouter fix |
| Step 0 killswitch check + sentinel | ✅ shipped |
| Gate liveness check | ✅ shipped (`octo-gate-liveness.ps1`) |
| PATH-scrubbed `/octo:review` | ✅ shipped (`octo-review-clean.ps1`) |
| Auto-merge decision (opt-in mechanism) | ✅ shipped (`octo-auto-merge-decision.ps1`) |
| Review verdict writer | ✅ shipped (`chatbot-review-write.ps1` + schema) |
| Gate ledger writer | ✅ shipped (`gate-ledger-write.ps1` + schema) |
| Cost tally | ✅ shipped (`octo-cost-tally.ps1`) |
| Loop killswitch | ✅ shipped (`loop-killswitch.ps1`) |
| **Tier 1 Demerzel integration** (GA → Demerzel status emission) | ✅ shipped (`project-sync.ps1` + `ga-loop-status.schema.json`) |
| **Tier 2 Demerzel integration** (Demerzel → GA directives) | ✅ shipped (`check-governance-directives.ps1` + `governance-directives.schema.json` + SKILL.md Step 0) |
| **Tier 3 Demerzel integration** (Demerzel orchestrates the loop) | 🟡 contract drafted (`docs/contracts/2026-05-10-ga-loop-driver.contract.md`); Demerzel-side `ga-loop-driver.ixql` pipeline not yet written |
| L3 default-on enablement | ❌ blocked on remaining checklist items (5 clean auto-merges, production canary, CI env fix) |
| L4 dark factory | ❌ blocked on remaining checklist items (telemetry pipeline, triage skill, scheduler, anomaly detection) |

Net: the autonomy *mechanism* is more complete than the autonomy *
authorization*. Every level above L2-opt-in needs evidence + infra
that can only be built incrementally as real PRs flow through.

## Related

- `.claude/skills/chatbot-iterate/SKILL.md` — the loop skill itself
- `.claude/skills/learnings/SKILL.md` — post-merge learning capture
- `Scripts/session-pr-check.ps1` — Stop-hook PR/CI summary
- `Scripts/check-chatbot-tribunal-gate.ps1` — path-based gate classifier
- `Scripts/octo-gate-liveness.ps1` — **gate liveness check** (exits 2 if
  all multi-LLM specialists failed silently — prevents false-green
  `{"findings": []}` from passing as "no issues"). Run as Step 4.5 of
  `/chatbot-iterate` per its SKILL.md.
- `Scripts/octo-review-clean.ps1` — **PATH-scrubbed `/octo:review`** for
  Windows. Strips space-bearing PATH entries before invoking the
  orchestrator, working around the v9.13.0 env-spawn bug that fails
  with `env: 'Files/NVIDIA': No such file or directory`. Use until the
  orchestrator is patched upstream.
- `BACKLOG.md` — chatbot items live in the curated **Chatbot Track**
  section, plus generic infra under "chatbot" / "TheoryAgent" / "MCP"
  / "intent routing" headings.
- `docs/solutions/` — accumulated learnings from past iterations
- `docs/solutions/tooling/2026-05-10-octo-plugin-install-corruption-silent-gate-failure.md`
  — the full diagnosis behind the two scripts above.
- `feedback_multi_llm_review_pays_off` (project memory) — the empirical
  argument for the gate. **Clarified 2026-05-10:** the "9 bugs caught"
  claim is real, but the proven mechanism is **direct subagent dispatch
  via the Agent tool** (`Agent(subagent_type: "octo:droids:octo-code-reviewer", ...)`),
  NOT the `/octo:review` slash command. The Agent-tool path goes
  through Claude Code's own subagent runtime; it's independent of the
  broken orchestrator and continues to work today. `/chatbot-iterate`
  Step 4 has been updated to use the Agent-tool path as primary,
  with `/octo:review` as an optional secondary path gated by the
  liveness check.
