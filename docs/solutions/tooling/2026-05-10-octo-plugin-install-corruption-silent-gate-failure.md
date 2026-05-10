---
title: "/octo:review silent failure: env unquoted PATH on Windows + doctor formatter bash bugs"
date: 2026-05-10
category: "tooling"
tags: [octopus, multi-llm-review, gate-broken, set-e-bash-bug, windows-path-spaces, env-127, false-green, iron-law]
symptoms: "/octo:review returns {\"findings\": []} with no errors; /octo:doctor exits 1 silently after printing only the banner; ALL recent reviews (≥7 over 11+ days) returned empty without anyone noticing"
components:
  - ~/.claude/plugins/cache/nyldn-plugins/octo/9.13.0/scripts/orchestrate.sh
  - ~/.claude/plugins/cache/nyldn-plugins/octo/9.13.0/scripts/lib/doctor.sh
  - ~/.claude-octopus/results/codex-review-r1-*-*.md
  - ~/.claude-octopus/results/gemini-review-r1-*-*.md
  - ~/.claude-octopus/results/review-findings-*.json
  - ~/.claude-octopus/provider-fallbacks.log
  - .claude/skills/chatbot-iterate/SKILL.md (Iron Law block)
severity: "load-bearing-infrastructure"
---

# /octo:review silent failure: Windows PATH spaces + doctor formatter bash bugs

## What the false-green looks like

`/octo:review <PR>` runs without error, prints "No issues found", and the agent reports a passing gate. **The verdict is meaningless** — the orchestrator dispatched the specialist agents, but their child processes fail at PATH resolution and never produce any output. The synthesizer correctly observes "no findings collected" and dutifully reports empty.

If the agent treats "no issues" as gate-green and merges, the Iron Law in `/chatbot-iterate` is violated without anyone noticing.

## Real root cause (corrected 2026-05-10 after misdiagnosis)

**Initial wrong diagnosis:** "75 plugin files missing." That was an artefact of my own bypass wrapper failing to set `$PLUGIN_DIR`, which made every hook-script-path resolve to `""` instead of the plugin root. With `PLUGIN_DIR` set correctly the doctor reports `pass=22 warn=5 fail=3 info=5` — plugin install is **healthy**.

**Actual root cause:** Windows PATH contains entries with spaces (`C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v13.0\bin`, `C:\Program Files (x86)\NVIDIA Corporation\PhysX\Common`, `C:\Program Files\dotnet`). The orchestrator's child-process spawn for codex/gemini agents passes PATH (or another env var) unquoted to `env`, so bash word-splits on the spaces. The first space-bearing entry becomes `Files/NVIDIA` (literal), `env` looks for an executable named `Files/NVIDIA`, fails with `No such file or directory`, exits 127.

Per-agent result file ends with:

```
## Status: FAILED (exit code: 127)
## Error Log
env: 'Files/NVIDIA': No such file or directory
```

Codex AND both Gemini specialists fail this way every time. Claude-sonnet (dispatched via Agent Teams not env-spawn) fails differently — its result file contains only dispatch headers, suggesting the SubagentStop hook either doesn't fire or doesn't write back. That's a separate bug worth investigating.

## Detection

Three signals, any of which is sufficient:

1. **Empty findings JSON across multiple recent runs.** `~/.claude-octopus/results/review-findings-*.json` files at 16–17 bytes (`{"findings": []}`) for ≥3 consecutive runs.
2. **Per-agent result files end with `Status: FAILED (exit code: 127)` and `env: 'Files/...'`** for codex/gemini, or contain ONLY dispatch headers (no review body) for claude-sonnet.
3. **`~/.claude-octopus/provider-fallbacks.log`** shows persistent `Round 1 agent failed` lines.

Quick check:

```sh
ls -la ~/.claude-octopus/results/review-findings-*.json | tail -5
# Anything ≤17 bytes is empty
```

```sh
# What did the agent actually produce?
LATEST=$(ls -t ~/.claude-octopus/results/codex-review-r1-*-*.md | head -1)
tail -10 "$LATEST"
# If it ends with "Status: FAILED (exit code: 127)" and the env error,
# you're hitting the Windows PATH bug.
```

## /octo:doctor itself doesn't surface this

Two `set -eo pipefail` bash bugs in v9.13.0 prevent the doctor from printing its diagnosis:

1. **`shift` on empty `$@`** in the `doctor` case branch (`orchestrate.sh:3342`) — when invoked as just `doctor` with no second arg, the shift exits 1 and `set -e` aborts. Workaround: pass any second arg (`doctor --verbose` or `doctor providers`).
2. **`(( pass_count++ ))`** in `doctor_output_human` (`lib/doctor.sh`) returns 0 when the counter starts at 0, which `set -e` treats as failure — the formatter dies before printing anything. Workaround: source `lib/doctor.sh` directly with `set +e`.

Even with both workarounds the doctor doesn't currently check for the env-127 failure mode — that check would have to be added.

## Fixes

### Fix 1: install order/quoting in the orchestrator (upstream)

The orchestrator's child-process spawn needs to quote PATH (and any other env it forwards) when invoking `env`. File this upstream at `nyldn/claude-octopus`. Possible patches:

- Replace `env CMD_PATH=...` invocations with proper `env -i` + selectively forwarded vars, quoted.
- Or strip space-bearing PATH entries before spawn.

### Fix 2: clean PATH for child processes (workaround in current shell)

```sh
export PATH=$(echo "$PATH" | tr ':' '\n' | grep -v ' ' | tr '\n' ':' | sed 's/:$//')
/octo:review <PR>
```

Removes ALL space-bearing PATH entries before invoking the orchestrator. Should let codex/gemini child processes spawn cleanly. May break tools that need NVIDIA / dotnet / etc. on PATH — scope to the review session only.

### Fix 3: bypass the orchestrator (immediate workaround)

Both providers are healthy, authenticated, and have working binaries. The orchestrator is the broken layer. Direct CLI invocation works:

```sh
gh pr diff <N> | codex review -
gh pr diff <N> | gemini  # then paste / use --review flag depending on version
```

You lose the orchestrator's debate / synthesis / auto-post features, but you get real findings.

## Detection wrapper for the bypass-to-doctor

A 25-line bash wrapper bypasses both formatter bugs and prints all 54 doctor checks raw — IF you set `PLUGIN_DIR` correctly (this was my mistake on the first pass):

```sh
#!/usr/bin/env bash
set +e
set +o pipefail
export CLAUDE_PLUGIN_ROOT="$HOME/.claude/plugins/cache/nyldn-plugins/octo/9.13.0"
SCRIPT_DIR="${CLAUDE_PLUGIN_ROOT}/scripts"
PLUGIN_DIR="${CLAUDE_PLUGIN_ROOT}"   # CRITICAL — without this every hook-path resolves to "" and you get false-positive "missing file" failures
export OCTOPUS_PLATFORM="$(uname)"
export OCTOPUS_HOST="claude"
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

Until the env-PATH bug is patched (or worked around per Fix 2), treat the Iron Law as having one functional gate (Demerzel tribunal) plus local tests, with `/octo:review` as advisory-only. After fix, run a known-good PR through `/octo:review` and verify the per-agent result files end with non-empty `## Output` sections (not `Status: FAILED`) before re-treating the gate as required.

The empirical "9 bugs caught in PR #151" claim in the `feedback_multi_llm_review_pays_off` memory predates this discovery (PR #151 review was around 2026-05-04/05; provider-fallbacks log shows the orchestrator was already broken by then). The 9 bugs may have been collected via direct `codex review` / `gemini review` CLI invocations rather than `/octo:review`. **Worth verifying** — if the 9-bug claim came from the same broken orchestrator, the Iron Law cost/benefit equation reverses.

## Lesson

**A multi-LLM review gate that returns empty is indistinguishable from a multi-LLM review gate that returns no real findings.** Need a liveness signal: each `/octo:review` should sanity-check before reporting verdict — at minimum, verify ≥1 specialist's result file does NOT end with `Status: FAILED (exit code: <N>)` before declaring the gate green.

The simplest version of that check, post-run:

```sh
LATEST=$(ls -t ~/.claude-octopus/results/review-findings-*.json | head -1)
TIMESTAMP=$(basename "$LATEST" .json | sed 's/^review-findings-//')
FAILED=$(grep -l "Status: FAILED" ~/.claude-octopus/results/*-review-r1-*-${TIMESTAMP}.md 2>/dev/null | wc -l)
TOTAL=$(ls ~/.claude-octopus/results/*-review-r1-*-${TIMESTAMP}.md 2>/dev/null | wc -l)
if [ "$TOTAL" -gt 0 ] && [ "$FAILED" -eq "$TOTAL" ]; then
  echo "GATE DARK — all $TOTAL specialists failed (exit 127 likely)"
fi
```

This is the cheapest version of "trust but verify" for the gate. Worth building into `/chatbot-iterate` Step 4 before treating any verdict as green.

## Misdiagnosis lesson

When bypassing tooling (e.g., sourcing internal libs to dodge a formatter bug), **replicate the full env the tool expects**. My initial wrapper set `CLAUDE_PLUGIN_ROOT` and `SCRIPT_DIR` but missed `PLUGIN_DIR`, leading to ~75 false-positive "missing file" failures. The correct numbers (`pass=22 warn=5 fail=3 info=5`) only emerged once `PLUGIN_DIR=$CLAUDE_PLUGIN_ROOT` was added. Set every variable the tool's source-of-truth would set, not just the obvious ones.
