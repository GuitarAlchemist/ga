---
name: test-plan
description: Diff-driven test-plan proposer for non-trivial PRs. Resolves the PR, categorizes each changed file by language/layer and test surface, weights by risk class (pure-additive / refactor / api-change / one-way-door), and emits a structured proposal of unit + integration + E2E + chatbot test cases keyed to the actual lines that changed. Writes to state/quality/test-plans/<head-sha>.md and posts a sticky PR comment. PROPOSES — never auto-writes test code; the human writes the tests.
allowed-tools: Bash, Read, Write, Grep, Glob
last_verified: 2026-05-23
karpathy_rule: R4-goal-driven-execution (every PR declares verifiable success criteria — a test plan IS those criteria)
related_plan: docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md
---

# /test-plan

Closes the **"developer forgot to write a test plan"** gap. Reads a PR diff,
maps each changed file to its likely test surface, and emits a structured set
of test cases the author (human or agent) should add before merge.

Invoked as `/test-plan` for the current branch's open PR, or
`/test-plan <PR#>` for a specific number.

Sister skills:

- `/grade-last-pr` — post-merge intent-vs-delivery (looks backward).
- `/council` — pre-merge gate for one-way doors (escalated review).
- `/test-plan` — pre-merge test-coverage proposer (this skill; daily use).

Together they form the **before / during / after** triangle of PR quality.

## When to run

- **Right after a PR opens** — the `.github/workflows/test-plan-suggester.yml`
  fires automatically on `opened` + `synchronize` if the PR body lacks a
  `## Test plan` section. Re-runs on every push to update the proposal.
- **Before requesting review** — if you wrote a thin `## Test plan` and want
  a second opinion on what else to test.
- **When grading an agent-authored PR low for coverage** — re-run after the
  agent fixes its plan, before merging.

**Do NOT** invoke for:

- Typo PRs (< 5 changed lines, no logic change) — overhead exceeds value.
- Pure-documentation PRs (only `*.md` outside tests/docs/contracts) — no
  test surface.
- Revert PRs — the test plan is "the prior PR's test plan, rerun" (use
  `/grade-last-pr` on the revert instead).
- Unmerged PRs you're about to abandon — wasted budget.

## What this skill does NOT do

- **Never auto-writes test code.** Writing tests requires understanding intent,
  fixture layout, and the team's test style — that's an engineering call, not
  a heuristic. A future `/test-write` skill could take the proposal as input;
  this skill stops at the proposal.
- **Never blocks merge.** The workflow posts a comment; it is advisory. The
  blocking gate for one-way doors is `/council`. For routine PRs, the review
  process is the gate.
- **Never edits the PR body.** Adds a sticky comment with marker
  `<!-- test-plan -->`. The author owns the body; the bot owns the comment.

## How to run

### 1. Resolve the PR

```bash
PR_NUM="${1:-$(gh pr view --json number --jq .number 2>/dev/null)}"
if [ -z "$PR_NUM" ]; then
  echo "No PR on current branch and no PR number passed. Aborting."
  exit 1
fi

gh pr view "$PR_NUM" --json title,body,baseRefName,headRefName,headRefOid,files,additions,deletions,url,author \
  > /tmp/test-plan-pr-$PR_NUM.json
```

Skip-suppression: if the PR body contains `[skip test-plan]` on its own line
(case-insensitive, with optional surrounding whitespace), exit clean with
`suppressed by author`. The marker must be on its own line so that PR bodies
that *describe* the skill (mentioning the marker inside a backtick code span)
do not accidentally suppress the workflow on themselves. This mirrors the
convention from other bot workflows.

### 2. Capture the diff

```bash
BASE=$(jq -r .baseRefName /tmp/test-plan-pr-$PR_NUM.json)
git fetch origin "$BASE" --depth=50 2>/dev/null || true
git diff --stat "origin/$BASE...HEAD" > /tmp/test-plan-stat-$PR_NUM.txt
git diff "origin/$BASE...HEAD" > /tmp/test-plan-diff-$PR_NUM.patch
```

Cap the per-file diff load at **100 KB** when reading into the prompt — large
generated files (lockfiles, snapshots) blow the budget and add no signal.
The stat output is always cheap; read it in full.

### 3. Categorize changes (the heuristic)

This is the **load-bearing heuristic** of the skill. It is intentionally
small — a few path-prefix matches and a couple of content regexes. Document
overrides inline so future humans can tune.

#### 3a. Language / layer (by path prefix)

| Path prefix | Layer | Default test type |
|---|---|---|
| `Common/GA.Business.Core/**/*.cs` | .NET domain | unit (xUnit) |
| `Common/GA.Business.ML/**/*.cs` | .NET ML pipeline | unit + integration (corpus fixture) |
| `Apps/ga-server/GaApi/Controllers/**/*.cs` | .NET HTTP API | integration (WebApplicationFactory) |
| `Apps/ga-server/GaApi/**/*.cs` (non-controller) | .NET API internals | unit |
| `Apps/ga-server/GaMcpServer/**/*.cs` | .NET MCP server | integration (MCP handshake) |
| `Common/GA.Business.DSL/**/*.fs` | F# DSL | unit (Expecto) |
| `ReactComponents/ga-react-components/src/**/*.{ts,tsx}` | TS/React SPA | unit (Vitest) + E2E (Playwright) for routes |
| `ReactComponents/ga-react-components/vite.config.ts` | Vite middleware | integration (curl the endpoint in dev mode) |
| `ReactComponents/ga-react-components/tests/**/*.spec.ts` | Existing E2E | the PR is itself a test — no proposal needed for that file |
| `Scripts/**/*.ps1` | PowerShell tooling | runbook smoke (manual) + `-WhatIf` lint |
| `.github/workflows/**/*.yml` | CI/CD | workflow lint (actionlint) + dry-run via `workflow_dispatch` |
| `docs/contracts/*.{md,json}` | Cross-repo contracts | schema validation + cross-repo integration test |
| `docs/plans/**/*.md` | Plan doc | none (planning artifact, not code) |
| `state/**/*.json` | State artifact | schema validation only |
| `**/*.md` (other) | Documentation | none |

#### 3b. Test surface (by content regex on the diff hunks)

Run cheap regexes against the diff to refine the per-file surface. Multiple
matches stack (a controller that calls a recognizer hits both rows).

| Regex (in added lines, `^+` prefix) | Surface refinement |
|---|---|
| `public\s+(class\|interface\|record)` (C#) or `export\s+(function\|const\|class)` (TS) | **API surface** — proposal MUST include a contract test |
| `\[HttpGet\|\[HttpPost\|\[Route\b` | **HTTP endpoint** — propose integration test against the route |
| `useState\|useEffect\|useMemo\b` | **React state** — propose Vitest component test |
| `<[A-Z][A-Za-z]+` in `.tsx` (new JSX element) | **Render surface** — propose Playwright assertion |
| `IRecognizer\|ICanonical\|ChordRecognizer\b` | **Recognizer path** — propose chatbot prompt covering it |
| `OpticK\|VoicingSearch\|EmbeddingSchema\b` | **OPTIC-K path** — propose voicing-search integration test |
| `await\s+\w+\.(GetAsync\|PostAsync)` | **External call** — propose mock test + timeout test |
| `throw\s+new\b` (added) | **New error path** — propose negative-case test |

If no regex hits, fall back to the path-prefix default.

#### 3c. Risk class (per-PR)

Pick **one** for the whole PR:

| Class | Criteria | Test-density multiplier |
|---|---|---|
| **pure-additive** | All changes are new files; no existing-file modifications; no deletions. | 1× |
| **refactor** | Renames, extracts, moves; tests likely still pass; behavior unchanged in intent. | 1.5× (regression catching) |
| **api-change** | Public surface modified (signature change in `public` C#, exported TS, controller route, contract schema). | 2× |
| **one-way-door** | Touches any path in `/council`'s door list (OPTIC-K dim, contract schema, public controller route, SPA route, installer). | 3× + flag for `/council` |

If `one-way-door`, the proposal MUST include a one-liner:

> **One-way-door touched** — consider running `/council <PR#>` before merge.

### 4. Generate the proposal

Write to **both** the session and `state/quality/test-plans/<head-sha>.md`.
Use the template below verbatim — section order matters because the comment
parser truncates at the `## Coverage gaps surfaced` header to fit GitHub's
65 KB comment limit on overgrown PRs.

```markdown
# Test plan — PR #<N> (<head_sha_short>)

**Generated:** <RFC3339 UTC>
**Author:** @<gh-login>
**Scope:** <one-sentence diff summary: "N files, +A/-D, touching <layers>">
**Risk class:** <pure-additive | refactor | api-change | one-way-door>

<one-way-door warning if applicable>

## Unit tests (N proposed)

- [ ] **<Namespace.Class>**: <one-line case description> (file: `<path>:<line>`)
  - Rationale: <why this case — cite the diff hunk in 1 sentence>
- [ ] ...

## Integration tests (N proposed)

- [ ] **<endpoint or boundary>**: <one-line case description>
  - Rationale: <why — what end-to-end path this exercises>
- [ ] ...

## E2E tests (Playwright) (N proposed)

- [ ] **<route or user flow>**: <expected assertion>
  - Rationale: <what the user sees that the PR changes>
- [ ] ...

## Chatbot prompts (N proposed)

- [ ] "<prompt text the chatbot should now handle correctly>"
  - Existing similar prompt: `state/quality/chatbot-qa/golden-traces/<closest>/...` (or "none yet")
  - Why now: <what new code path this PR adds that could regress this prompt>
- [ ] ...

## Coverage gaps surfaced

- **No test covers** `<path>:<line>` (<reason — new branch, new error path, new public API>)
- ...

## Rubric

This plan was generated by **/test-plan** using:
- **Diff-driven**: proposals are keyed to changed lines, not the whole module.
- **Surface-aware**: each layer maps to its canonical test type (unit → integration → e2e → chatbot).
- **Risk-weighted**: <risk class> PRs get <multiplier>× the baseline density.
- **Coverage-gap-aware**: new code paths with no existing test are called out explicitly.

The heuristic itself lives in `.claude/skills/test-plan/SKILL.md` §3.
Override with judgment — the proposal is advisory, not prescriptive.
```

Density baseline: 1 unit case per public method touched; 1 integration case
per endpoint or boundary; 1 E2E per user-visible route; 1 chatbot prompt per
recognizer/OPTIC-K-touching diff. Multiply by the risk class.

Cap each section at **10 items**. If you have more, the heuristic is firing
on a refactor — pick the highest-risk ones and note `+N more candidates not
listed` at the bottom of the section.

### 5. Write the artifact

```bash
mkdir -p state/quality/test-plans
OUT="state/quality/test-plans/${HEAD_SHA}.md"
# Write the markdown to $OUT.

# Sidecar JSON with structured metadata (for trend analysis):
META="state/quality/test-plans/${HEAD_SHA}.meta.json"
cat > "$META" <<EOF
{
  "schema": "test-plan-v1",
  "pr_number": ${PR_NUM},
  "head_sha": "${HEAD_SHA}",
  "risk_class": "${RISK_CLASS}",
  "proposal_counts": {
    "unit": ${UNIT_COUNT},
    "integration": ${INTEGRATION_COUNT},
    "e2e": ${E2E_COUNT},
    "chatbot": ${CHATBOT_COUNT},
    "coverage_gaps": ${GAP_COUNT}
  },
  "layers_touched": [${LAYERS_JSON}],
  "generated_at": "${NOW_UTC}",
  "generator": "claude-opus-4-7"
}
EOF
```

Validate against `state/quality/test-plans/SCHEMA.json` before writing the
meta file — if `jq` or `ajv` is available, lint it. Otherwise, eyeball
required fields.

### 6. Post the PR comment

```bash
gh pr comment "$PR_NUM" --body "$(cat <<EOF
<!-- test-plan -->
## Proposed test plan (heuristic)

$(cat state/quality/test-plans/${HEAD_SHA}.md)

---

_Generated by [\`/test-plan\`](.claude/skills/test-plan/SKILL.md) — see \`state/quality/test-plans/${HEAD_SHA}.md\` for the persisted artifact. To suppress, add \`[skip test-plan]\` to the PR body._
EOF
)"
```

If a comment with the `<!-- test-plan -->` marker already exists, **update
it in place** rather than appending a new one — the sticky-comment pattern
keeps the conversation readable. The workflow uses `actions/github-script`
to handle the upsert; standalone runs from a session use:

```bash
EXISTING=$(gh pr view "$PR_NUM" --json comments \
  --jq '.comments[] | select(.body | startswith("<!-- test-plan -->")) | .url' | head -1)
if [ -n "$EXISTING" ]; then
  COMMENT_ID="${EXISTING##*#issuecomment-}"
  gh api -X PATCH "repos/{owner}/{repo}/issues/comments/$COMMENT_ID" \
    -f body="$NEW_BODY"
else
  gh pr comment "$PR_NUM" --body "$NEW_BODY"
fi
```

### 7. Print the terminal summary

One concise block. Keep it under 12 lines.

```
PR #321 test plan · risk: refactor
  base → head: main → feat/qa-tab-render-quality-readme @ 04476aac
  files:       2 changed, +74/-1
  layers:      TS/React (1), Vite middleware (1)

  proposed:
  • 3 unit  • 2 integration  • 2 E2E  • 0 chatbot
  • 1 coverage gap surfaced

  artifact: state/quality/test-plans/04476aac9972a8771fbfd36f297faaa1460e465b.md
  comment:  https://github.com/GuitarAlchemist/ga/pull/321#issuecomment-...
```

## Edge cases

- **PR with > 100 changed files.** Group by directory in the proposal; cap
  total cases at 30. A PR that big needs splitting, not testing.
- **PR with only generated files.** (lockfile bumps, schema regen) — exit
  with `no test surface; generated changes only`. Don't post a comment.
- **PR opened by a bot (dependabot/renovate).** Skip; bot author detection
  matches `/-bot$/` or `dependabot` in `author.login`. Bot PRs have their
  own gates.
- **Draft PR.** Run anyway — the proposal helps the author firm up the plan
  before ready-for-review. Mark `draft: true` in the sidecar JSON.
- **Force-push that rewrites history.** New `head_sha` → new artifact. The
  prior artifact stays for audit history (mirrors `/council`'s convention).
- **Body has `## Test plan` but it's empty / a checkbox stub.** Treat as
  missing; post the proposal as a starter draft for the author to flesh out.
- **PR from a known agent author** (Co-Authored-By Claude / Codex / Mercury
  in any commit message in the range). Post the proposal as a **second
  opinion** even if `## Test plan` exists — agents are the most likely to
  miss coverage, and the cost of a duplicate proposal is one comment.

## Anti-patterns

- **Proposing tests for unchanged code.** The whole point of "diff-driven" is
  that the cases hang off the diff. If you find yourself listing tests for
  a module just because it exists, you've drifted into "audit" — wrong
  skill, use `/octo:review`.
- **Proposing exhaustive coverage.** A test plan for a 2-file diff that
  lists 40 cases is noise. The author will skip the comment. Cap at the
  density-baseline × risk-class multiplier; if it overflows, the section
  cap (10) kicks in.
- **Auto-generating test code.** The skill MUST stop at the proposal. Test
  code is intent + fixtures + style — a future skill, not this one.
- **Re-posting on every push without sticky-comment dedup.** Spam. Use the
  marker upsert.
- **Treating the proposal as a checklist gate.** This skill is advisory. The
  blocking gates are `/council` (one-way doors) and the review process
  (everything else).
- **Running on the AGENTS.md / CLAUDE.md PR.** No code surface; exit clean.

## Heuristic tuning notes

The path-prefix table and the content regex table in §3 are the leverage
points. To tune:

1. Find a PR where the proposal was wrong (too many or too few cases).
2. Identify whether the miss was a *path* mismatch (added a new layer) or
   a *content* mismatch (new pattern that should hit one of the regexes).
3. Edit §3a or §3b in this file.
4. Note the change in the commit message; the heuristic is a one-way door
   in spirit (drift in test density is a real cost) — small additive tweaks
   are fine, but bulk rewrites should be plan-reviewed.

The sidecar JSON's `proposal_counts` field exists precisely so we can
graph proposal density over time and notice when the heuristic starts
over- or under-firing for a given layer.

## Why this exists

Karpathy R4: "Task completed != goal achieved." A test plan is the
operational form of "goal" — it declares the criteria the PR's author
believes the change satisfies, in the form of cases the team can mechanize.

Today, every PR has a `## Test plan` checklist in the template, but:

- Humans skip it ("I'll add tests after merge").
- Agents under-fill it (the prompt cap on PR-body generation truncates
  before the test plan).
- Reviewers don't enforce it (no automated gate; "looks fine, merging").

`/test-plan` makes the proposal cheap to generate and visible at PR time,
shifting the test-design conversation **left** of merge. The artifact is
persisted so the next session can read what was proposed vs what landed
(another input for `/grade-last-pr`).

The skill PROPOSES; the human writes. The skill is read-only on the codebase
(comments only on the PR). Both properties are load-bearing — they keep the
skill cheap, fast, and trust-worthy.

## Related

- `/grade-last-pr` (`.claude/skills/grade-last-pr/SKILL.md`) — post-merge
  intent grade. A `medium` or `low` grade frequently traces back to a thin
  test plan; this skill closes that gap upstream.
- `/council` (`.claude/skills/council/SKILL.md`) — escalated gate for
  one-way-door PRs. This skill's `one-way-door` risk class auto-suggests
  invoking it.
- `/backlog-groom` (`.claude/skills/backlog-groom/SKILL.md`) — proposes
  next work; this skill helps verify what just got picked.
- `state/quality/test-plans/README.md` — artifact directory contract.
- `state/quality/test-plans/SCHEMA.json` — sidecar JSON Schema
  (`test-plan-v1`).
- `.github/workflows/test-plan-suggester.yml` — the auto-fire workflow.
- `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md` —
  parent plan; this skill is a Phase 2+ harness item.
