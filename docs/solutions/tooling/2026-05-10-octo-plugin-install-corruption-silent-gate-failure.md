---
title: "Octo plugin install corruption causes silent /octo:review failure (false-green gate)"
date: 2026-05-10
category: "tooling"
tags: [octopus, multi-llm-review, gate-broken, set-e-bash-bug, plugin-install, claude-code, false-green, iron-law]
symptoms: "/octo:review returns {\"findings\": []} with no errors; /octo:doctor exits 1 silently after printing only the banner; ALL recent reviews (≥7 over 11+ days) returned empty without anyone noticing"
components:
  - ~/.claude/plugins/cache/nyldn-plugins/octo/9.13.0/scripts/orchestrate.sh
  - ~/.claude/plugins/cache/nyldn-plugins/octo/9.13.0/scripts/lib/doctor.sh
  - ~/.claude/plugins/cache/nyldn-plugins/octo/9.13.0/hooks/subagent-result-capture.sh (MISSING)
  - ~/.claude-octopus/results/review-findings-*.json
  - ~/.claude-octopus/provider-fallbacks.log
  - .claude/skills/chatbot-iterate/SKILL.md (Iron Law block)
severity: "load-bearing-infrastructure"
---

# Octo plugin install corruption causes silent /octo:review failure

## What the false-green looks like

`/octo:review <PR>` runs without error, prints "No issues found", and the agent reports a passing gate. **The verdict is meaningless** — the orchestrator never collected any findings because critical hook scripts referenced by its manifest are missing from the plugin install. The synthesizer correctly observes "no findings collected" and dutifully reports empty.

If the agent treats "no issues" as gate-green and merges, the Iron Law in `/chatbot-iterate` is violated without anyone noticing.

## How to detect

Three signals, any of which is sufficient:

1. **Empty findings JSON across multiple recent runs.** `~/.claude-octopus/results/review-findings-*.json` files at 16–17 bytes (`{"findings": []}` or `{"findings":[]}`) for ≥3 consecutive runs.
2. **Provider-fallbacks log shows persistent Round-1 failures.** `~/.claude-octopus/provider-fallbacks.log` lines like `provider=codex status=fallback detail=Round 1 agent failed` repeating across days.
3. **Per-agent result files contain only headers.** `~/.claude-octopus/results/claude-sonnet-review-r1-*.md` should contain the actual review output. If it's just `# Agent: ... # Task ID: ... # Started: ...` with no review body, the SubagentStop hook never wrote anything.

## Why it happens

The on-disk plugin install at `~/.claude/plugins/cache/nyldn-plugins/octo/9.13.0/` is missing ~75 files referenced in its own manifests:

- ~25 hook scripts (most critical: `subagent-result-capture.sh` — the SubagentStop hook that pipes Claude Code agent-team output into the orchestrator's results JSON)
- ~28 skill markdown files
- ~25 command markdown files
- `agents/config.yaml`

Cause is unknown — likely an aborted install or a partial sync that the package manager didn't catch. The plugin reports v9.13.0 and the providers (codex, gemini, ollama) are individually healthy and authenticated; only the *plugin's wiring* is broken.

## Why /octo:doctor itself doesn't surface this

Two `set -eo pipefail` bash bugs in v9.13.0 prevent the doctor from printing its diagnosis:

1. **`shift` on empty `$@`** in the `doctor` case branch (line 3342 of `orchestrate.sh`) — when invoked as just `doctor` with no second arg, the shift exits 1 and `set -e` aborts. Workaround: pass any second arg (`doctor --verbose` or `doctor all`).
2. **`(( pass_count++ ))`** in `doctor_output_human` returns 0 when the counter starts at 0, which `set -e` treats as failure — the formatter dies before printing anything. Workaround: source `lib/doctor.sh` directly with `set +e`.

So even when you suspect the gate is broken, the diagnostic tool fails the same way silently.

## Fix

```sh
claude plugin uninstall octo
claude plugin install octo@nyldn-plugins
```

Then re-run `/octo:doctor --verbose`. Confirm hook + skill + command file counts are back to expected (no `[fail] hooks` or `[fail] skills` rows).

If reinstall doesn't help, the standalone install at `~/.claude/plugins/cache/nyldn-plugins/octo/<version>/` may need to be deleted manually before reinstall — the package manager doesn't always clean up partial installs.

## Detection wrapper for future use

A 25-line bash wrapper bypasses both formatter bugs and prints all 54 doctor checks raw:

```sh
#!/usr/bin/env bash
set +e
set +o pipefail
export CLAUDE_PLUGIN_ROOT="$HOME/.claude/plugins/cache/nyldn-plugins/octo/9.13.0"
SCRIPT_DIR="${CLAUDE_PLUGIN_ROOT}/scripts"
source "${SCRIPT_DIR}/lib/common.sh" 2>/dev/null
source "${SCRIPT_DIR}/lib/doctor.sh" 2>/dev/null
DOCTOR_RESULTS_NAME=()
DOCTOR_RESULTS_CAT=()
DOCTOR_RESULTS_STATUS=()
DOCTOR_RESULTS_MSG=()
DOCTOR_RESULTS_DETAIL=()
for fn in doctor_check_providers doctor_check_auth doctor_check_config \
          doctor_check_state doctor_check_smoke doctor_check_hooks \
          doctor_check_scheduler doctor_check_skills doctor_check_conflicts \
          doctor_check_agents doctor_check_recurrence; do
  declare -f "$fn" > /dev/null && "$fn" 2>/dev/null
done
for ((i=0; i<${#DOCTOR_RESULTS_NAME[@]}; i++)); do
  printf "%-8s %-12s %-30s %s\n" \
    "[${DOCTOR_RESULTS_STATUS[i]}]" \
    "${DOCTOR_RESULTS_CAT[i]}" \
    "${DOCTOR_RESULTS_NAME[i]}" \
    "${DOCTOR_RESULTS_MSG[i]}"
done
```

Save as `Scripts/octo-doctor-bypass.sh` if you want this on hand without depending on a future doctor fix.

## Implications for the chatbot-iterate Iron Law

`.claude/skills/chatbot-iterate/SKILL.md` documents that PRs touching `Common/GA.Business.ML/Agents/`, MCP, DSL, etc. **REQUIRE** `/octo:review` + Demerzel tribunal before merge. The "REQUIRED" framing is load-bearing on `/octo:review` actually running.

Until the orchestrator is fixed, treat the Iron Law as having one functional gate (Demerzel tribunal) plus local tests, with `/octo:review` as advisory-only. After fix, run a known-good PR through `/octo:review` and verify the per-agent result files contain real review content (not just dispatch headers) before re-treating the gate as required.

The empirical "9 bugs caught in PR #151" claim in the `feedback_multi_llm_review_pays_off` memory predates this corruption (PR #151 review was around 2026-05-04/05; provider-fallbacks log shows the orchestrator was already broken by then, but the 9 bugs may have been collected via direct `codex review` / `gemini review` CLI invocations rather than `/octo:review`). Worth verifying — if the 9-bug claim came from the same broken orchestrator, the Iron Law cost/benefit equation reverses.

## Workaround for individual reviews while the orchestrator is broken

```sh
gh pr diff 155 | codex review        # direct Codex CLI — bypasses orchestrator
gh pr diff 155 | gemini review       # direct Gemini CLI
```

Both authenticated providers (per doctor) and have working binaries. They produce real findings; the orchestrator was the broken layer.

## Lesson

**A multi-LLM review gate that returns empty is indistinguishable from a multi-LLM review gate that returns no real findings.** Need a liveness signal: either each `/octo:review` should ALSO assert "≥1 specialist returned content" before reporting verdict, OR the gate consumer (`/chatbot-iterate`) should sanity-check `provider-fallbacks.log` before trusting the result.

The simplest version of that check: count non-empty per-agent result files. If `find ~/.claude-octopus/results -name "claude-*-review-r1-*.md" -newer <findings.json> -size +1k | wc -l` is 0 for the latest run, the gate is dark.
