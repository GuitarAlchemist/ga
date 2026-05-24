---
name: grade-last-pr
description: Post-merge intent-vs-delivery evaluator. Resolves the most recent squash-merged PR, captures its stated intent (title + Summary), diffs it against what actually landed, runs /octo:review for specialist commentary, scans Codex bot comments (P0/P1 degrade the grade), computes a high/medium/low alignment score, and writes a structured grade to state/quality/pr-grades/<merge-sha>.json. Closes the "agent declared done, nobody checked the goal" loop.
allowed-tools: Bash, Read, Write, Skill
last_verified: 2026-05-24
karpathy_rule: R4-goal-driven-execution (task complete != goal achieved)
---

# /grade-last-pr

Grades the last merged PR on the current branch's upstream against the goal the author stated. Outputs a JSON grade card + a terminal summary.

Part of item #5 in `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md` — "Cybernetic governance loops". Today, an agent declares "done" and that's the end of the loop. This skill closes the loop by re-reading the intent after merge and comparing it to the diff.

## When to run

- **Immediately after a PR merges to main** — fresh memory, clean diff.
- **Inside the post-merge smoke action** (once item #4 lands, this skill can be invoked from CI as a follow-up step — but works standalone today).
- **Spot-checking an agent's work** — pick any merge SHA and grade it.

**Do NOT** invoke for unmerged PRs (the diff isn't stable yet — use `/octo:review` instead) or for revert commits (use `/learnings` to capture the regression).

## How to run

### 1. Resolve the last merged PR

```bash
# Latest merge on this branch
git log -1 --format='%H %s'
# Cross-check against gh's view of merged PRs
gh pr list --state merged --limit 1 --json number,title,mergeCommit,mergedAt,headRefName
```

The merge SHA from `git log` should equal `mergeCommit.oid` from `gh`. If they disagree, the local branch is ahead of what gh sees — `git fetch origin` and retry, or pass an explicit PR number as the argument (`/grade-last-pr 308`).

Squash-merge convention means `HEAD` is the merged commit itself (one commit per PR). For merge-commit PRs the same logic still works; `git show HEAD --stat` reports the merge.

### 2. Capture the stated intent

```bash
gh pr view <num> --json title,body,number,mergedAt,mergeCommit,headRefName,author
```

The **stated intent** is `title` plus the `## Summary` section of `body` (the first `##` block, up to the next `##`). If the body has no `## Summary`, fall back to the title and flag `intent_section_missing` in `reasons`.

Skip everything below `## Test plan` — that's how the PR was verified, not what it was for.

### 3. Capture the actual diff

```bash
git show HEAD --stat       # files changed + line counts
git show HEAD --name-only  # just the file list (cleaner for the JSON field)
```

For the specialist review (step 4) pipe the full unified diff:

```bash
git show HEAD
```

If the PR is huge (> 500 changed lines or > 50 files), summarize per-directory line counts instead — the specialist review tools have token caps.

### 4. Run /octo:review against the merged diff

Invoke the existing `/octo:review` skill (`plugin:octo:review`) with the PR title + Summary as the "intent" frame and the unified diff as the target. Set `target=specific path` with the merge SHA, `focus=all`, `autonomy=autonomous`, `publish=skip` (this is a grading pass, not an inline-comment pass).

Capture the specialists' verdicts — at minimum `code-reviewer` and `security-sentinel` if present in the output — for the `specialist_notes` field. If `/octo:review` returns its multi-AI fleet output, take the synthesis section verbatim (one paragraph) for each named specialist.

If `/octo:review` is unavailable in the current session (no `plugin:octo:review` in the skill list), record `"specialist_notes": {"unavailable": "octo:review skill not loaded; grade based on diff inspection only"}` and proceed with a Claude-only review pass.

### 5. Scan Codex bot review comments

Codex (`chatgpt-codex-connector[bot]`) leaves inline review comments on PRs that are **not** surfaced in the standard `gh pr view` merge flow. We've shipped PRs with unresolved Codex findings before (e.g. PR #308's `mkdir -p .claude/local` fix), so this step is a hard requirement, not advisory.

```bash
REPO="GuitarAlchemist/ga"
gh api "repos/$REPO/pulls/$PR/comments" \
  --jq '.[] | select(.user.login == "chatgpt-codex-connector[bot]")'
```

For each Codex comment, extract:

- **Priority** — parse from the `![P{0,1,2,3} Badge]` markdown shield at the start of `body`. The regex `!\[P([0-3]) Badge\]` is reliable; if the shield is missing, treat priority as `?` and surface in `reasons`.
- **Title** — the bold heading after the badge (the `**<sub>…</sub>  Title text**` line).
- **Body excerpt** — the first paragraph of the comment (everything up to the first blank line after the title).
- **Resolution status** — heuristic, in this order:
  1. Any `+1` / thumbs-up reaction from a human → `acknowledged`.
  2. Any reply comment on the same `pull_request_review_id` (or a `path`+`line` match) authored by a non-bot → `replied`.
  3. Any commit on the PR with `created_at > comment.created_at` → `possibly_addressed` (weak signal — still flag for human review unless commit message references the fix).
  4. Otherwise → `unresolved`.

Aggregate the counts and emit a "Codex review" block in the grade card:

```json
"codex_review": {
  "status": "reviewed",
  "comments_total": 1,
  "unresolved": { "P0": 0, "P1": 0, "P2": 1, "P3": 0 },
  "findings": [
    {
      "priority": "P2",
      "title": "Create `.claude/local` before copy instruction",
      "path": ".claude/local-template/README.md",
      "line": 23,
      "status": "unresolved",
      "url": "https://github.com/GuitarAlchemist/ga/pull/308#discussion_r3293624339",
      "excerpt": "The setup command in this README fails on a fresh checkout because `.claude/local/` is gitignored…"
    }
  ]
}
```

If the API returns no Codex comments at all, record `"codex_review": { "status": "not_reviewed", "comments_total": 0 }` — Codex hasn't visited the PR yet (or isn't installed). Don't degrade the grade for this.

**Codex-driven grade adjustment** — apply AFTER computing the alignment in step 6, but record the adjustment in `reasons`:

| Unresolved | Effect on grade |
|---|---|
| Any **P0** | Force `alignment = "low"` regardless of intent-vs-delivery match. Surface every P0 comment body verbatim in `reasons`. |
| Any **P1** | Degrade one tier: `high → medium`, `medium → low`. Cite the P1 titles in `reasons`. |
| **P2** / **P3** only | Informational. Quote the titles in `reasons` so the operator sees them; do not change the tier. |

This is the **Codex review gate**: a `low` grade with a P0/P1 attached is a signal to revert or to fast-follow with a fix PR, not just a number in an artifact.

### 5b. AI annotation hygiene (ai-annotations gate)

The ai-annotations campaign (`docs/contracts/2026-05-24-ai-annotation.contract.md` in ix) drops `@ai:invariant`, `@ai:assumption`, etc. markers in source. When the reconciler runs, it writes a report to `state/quality/ai-annotations-reconciliation.json` (path is shared across repos; ga sees the ix-produced file when run side-by-side, or copies it via the federation pattern).

Before computing the grade in step 6:

```bash
RECON="state/quality/ai-annotations-reconciliation.json"
if [ -f "$RECON" ]; then
  CHANGED_FILES=$(git show HEAD --name-only --pretty=format: | tr -d '\r')
  # Pull annotations that live in any of the changed files
  RELEVANT=$(jq --argjson files "$(echo "$CHANGED_FILES" | jq -R -s 'split("\n") | map(select(length > 0))')" \
    '.annotations | map(select(.location.path as $p | $files | index($p)))' "$RECON")
  C_COUNT=$(echo "$RELEVANT" | jq '[.[] | select(.truth_value == "C")] | length')
  F_COUNT=$(echo "$RELEVANT" | jq '[.[] | select(.truth_value == "F" and (.reconciliation.test_match // null) == null)] | length')
  STALE_COUNT=$(echo "$RELEVANT" | jq '[.[] | select(.stale == true)] | length')
fi
```

**Annotation-driven grade adjustment** — apply AFTER Codex adjustment, record in `reasons`:

| Unresolved in changed files | Effect on grade |
|---|---|
| Any **C** (contradictory) annotation | Degrade one tier (`high → medium`, `medium → low`). Cite the contradicting annotations in `reasons` with `path:line` references. |
| Any **F** (refuted) annotation without a `dismissed` certainty | Degrade one tier. Cite which claims got refuted. |
| Any **stale** annotation (file mtime > annotation.updated_at + 7d) | Informational. List in `reasons` so the author can refresh; do not change the tier. |

Record under `ai_annotations` in the grade card:

```json
"ai_annotations": {
  "status": "reviewed",
  "in_changed_files": 12,
  "unresolved": { "C": 0, "F": 1, "stale": 2 },
  "findings": [
    {"truth_value": "F", "kind": "invariant", "path": "src/foo.rs", "line": 42, "claim": "..." }
  ]
}
```

If `ai-annotations-reconciliation.json` doesn't exist, set `"ai_annotations": { "status": "not_run" }` and skip the adjustment — don't penalize PRs in a repo where the campaign hasn't shipped yet.

### 6. Compute the alignment score

Three buckets — no in-between. Be honest, not generous.

| Score | When |
|---|---|
| **high** | Diff matches stated intent. No surprise files. Tests/types are green (no new failures introduced). Scope is what the title says. |
| **medium** | Diff matches the intent's spirit but introduces something not called out in the Summary — e.g., a helper function, a tangential rename, a config tweak. The work isn't *wrong*, but the PR description undersold it. |
| **low** | Diff drifts from intent. E.g., title says "fix typo" but the diff refactors 3 files; or Summary promises a feature and the diff only adds tests; or the PR title and the changed file list have no overlap. |

Tie-breaker examples — these are the calls that matter most:

- **Refactor smuggled into a fix.** Title: "fix: handle null in X". Diff: fixes X + renames 4 unrelated symbols. → **medium** (renames weren't promised). Use **low** if the rename touches > 50% of changed lines.
- **Test-only PR with a "fix:" prefix.** Title says fix, diff is `*.test.ts` only. → **medium** (consider tests as evidence of a fix; flag if no production-code change accompanies a "fix:").
- **Doc PR that touches code.** Title: "docs: update README". Diff: README + one config change. → **low** (config change is a one-way door; should be a separate PR).
- **Multi-feature PR with a single-feature title.** → **low**.
- **Stated feature ships + a Karpathy-rule self-improvement gets bundled.** → **medium** (legitimate compound work, but the title hid the rule change).

### 7. Write the grade card

To `state/quality/pr-grades/<merge-sha>.json` — the **full** SHA, no truncation, so it sorts alphabetically and never collides with a short-SHA prefix.

```json
{
  "schema": "pr-grade-v1",
  "pr_number": 308,
  "merge_sha": "abc123def456...",
  "merged_at": "2026-05-23T23:35:20Z",
  "title": "feat(harness): /grade-last-pr skill",
  "stated_intent": "Adds a /grade-last-pr skill that grades merged PRs against stated intent and writes the result to state/quality/pr-grades/. Closes item #5 of the harness plan.",
  "actual_files_changed": [
    ".claude/skills/grade-last-pr/SKILL.md",
    "state/quality/pr-grades/README.md",
    "state/quality/pr-grades/SCHEMA.json",
    "state/harness/items.json",
    "docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md"
  ],
  "alignment": "high",
  "reasons": [
    "Diff matches stated intent: skill file + artifact dir + harness state update.",
    "No surprise files outside the plan's PR shape.",
    "Frontmatter declares allowed-tools and karpathy_rule per skill convention."
  ],
  "specialist_notes": {
    "code-reviewer": "Markdown-only addition; no executable changes; zero TS regression risk.",
    "security-sentinel": "Skill reads gh CLI + git output, no network egress, no eval."
  },
  "codex_review": {
    "status": "reviewed",
    "comments_total": 0,
    "unresolved": { "P0": 0, "P1": 0, "P2": 0, "P3": 0 },
    "findings": []
  },
  "graded_at": "2026-05-23T23:50:00Z",
  "grader": "claude-opus-4-7"
}
```

Validate against `state/quality/pr-grades/SCHEMA.json` before writing — if you have `npx ajv` or `jq` available, lint the JSON. Otherwise, eyeball the required fields list.

Schema notes:
- `schema` is the literal string `pr-grade-v1`. Bump to `pr-grade-v2` only when fields are removed or renamed (additive changes don't need a bump — readers must tolerate unknown fields).
- `merge_sha` is the **full 40-char** SHA so file sorts are chronological and unambiguous.
- `merged_at` and `graded_at` are RFC3339 UTC.
- `alignment` is one of `"high"`, `"medium"`, `"low"` — no other values.
- `reasons` is 1–5 short sentences. If you can't articulate a reason, the grade isn't ready.
- `grader` is the model name (free-form string; example: `"claude-opus-4-7"`).
- `codex_review` is optional but **strongly recommended**. Set `status` to `"reviewed"` if the API returned any Codex comments (even zero unresolved), `"not_reviewed"` if Codex hasn't visited yet, or `"skipped"` if the API call failed (record the error in `reasons`).

### 8. Print the terminal summary

One concise block. Keep it under 10 lines.

```
PR #308 graded · alignment: high
  merge:   5bb745b0  (2026-05-23T23:35:20Z)
  title:   feat(harness): /grade-last-pr skill
  changed: 5 files, +312/-2
  codex:   reviewed · P0=0 P1=0 P2=0 P3=0 unresolved

  reasons:
  • Diff matches stated intent: skill + artifact dir + state bump.
  • No surprise files outside the plan's PR shape.
  • specialist_notes/code-reviewer: no TS regression risk.

  grade card: state/quality/pr-grades/5bb745b0bb2d643dbabce06dd56bdaedcc887542.json
```

Surface the absolute path to the JSON in the last line so the user can `cat` it without rebuilding it.

If Codex degrades the grade, the block should show the new tier, the original tier, and the offending comment titles — e.g.:

```
PR #308 graded · alignment: medium  (Codex-degraded from high)
  merge:   abc12345  (2026-05-23T23:35:20Z)
  title:   feat(harness): adopt .claude/local/state.md pattern
  changed: 3 files, +84/-0
  codex:   reviewed · P0=0 P1=0 P2=1 P3=0 unresolved
    P2: Create `.claude/local` before copy instruction
        .claude/local-template/README.md:23
        https://github.com/GuitarAlchemist/ga/pull/308#discussion_r3293624339

  reasons:
  • Diff matches stated intent: README + template + state-md fixture.
  • Codex P2 (unresolved): mkdir step missing from setup snippet — informational only, no tier change.
```

(P2 example shows the *informational* path — no tier change. If a P1 were unresolved, the first line would read `alignment: medium (Codex-degraded from high)` and `reasons` would explain the demotion.)

## Edge cases

- **Detached HEAD or non-main branch.** Use `gh pr list --state merged --base main` to filter, or accept the argument override (`/grade-last-pr 308`).
- **PR with no `## Summary` section.** Fall back to title; add `intent_section_missing` to `reasons` so the human knows the grade is title-only.
- **Bot-merged PR (dependabot, renovate).** Treat as a separate class — grade still applies but `grader_notes` should call out the bot author. Auto-bumps usually score `high`.
- **Squash with edited commit message.** GitHub sometimes rewrites the merge subject. Use the PR title from `gh pr view`, not the commit subject.
- **Multiple PRs merged in the same minute.** `git log -1` and `gh pr list --limit 1` may disagree. Cross-check the SHA. If still ambiguous, accept the merge SHA as an argument.
- **Codex API rate-limited or unreachable.** Set `codex_review.status = "skipped"` and proceed; do not block grading on Codex availability. Surface the failure in `reasons` so the operator can re-run.
- **Codex comment with no badge shield.** Some older Codex comments lack the `![P{0-3} Badge]` shield. Treat as `priority = "?"` and surface in `reasons`. Do not silently downgrade — flag for human review.
- **Codex comment with an empty body or only an image.** Skip and continue; record in `findings` with `priority = "?"`, `excerpt = "(empty body)"`.

## Anti-patterns

- **Grade inflation.** Don't default to `high` when the diff "kind of" matches. The whole point of this skill is to surface drift — graded everything `high` and the loop tells you nothing.
- **Grading without reading the diff.** The skill must actually `git show HEAD`. A grade based on the PR description alone is theatre.
- **Grading agent-authored PRs more leniently than human-authored ones.** Same bar. The agent gets the same `low` you'd give a human who shipped scope creep.
- **Re-writing existing grade cards on re-invocation.** If `<sha>.json` already exists, append a `regraded_at` timestamp and an entry in `prior_grades[]` rather than overwriting silently — drift in grading itself is signal.
- **Editing the PR after grading.** The grade is a snapshot of the merged state. If the PR gets reverted, that's a *new* event for a *new* grade card (on the revert SHA).

## Why this exists

Karpathy R4: "Task completed != goal achieved." Today, when an agent finishes a PR, no automated system asks "did the diff actually deliver what the title promised?" Humans skim the title and the green CI checkmark and merge. Drift accumulates silently — a "fix typo" PR refactored three modules; a "feat: add X" PR added X but also broke Y in a way the tests didn't catch.

`/grade-last-pr` is the cheapest possible closing of that loop:
- **Generator-evaluator separation** — Cherny's pattern: the agent that wrote the PR is not the agent that grades it. The grader reads only the diff + the stated intent, not the conversation that produced them.
- **Append-only ledger** — `state/quality/pr-grades/` becomes the audit trail. Over weeks, a histogram of `high/medium/low` reveals whether the team's intent-articulation is tightening (more `high`) or eroding (more `medium`).
- **Cheap enough to run on every merge** — markdown skill + 2 git commands + 1 gh CLI call + 1 skill dispatch. Sub-second on a normal PR, < 30 s with `/octo:review` engaged.

## Related

- `/octo:review` — the multi-LLM review skill this skill dispatches.
- `/digest` — captures session state; complementary (digest is forward-looking, grade is backward-looking).
- `/learnings` — captures surprises into `docs/solutions/`; if a grade comes in `low`, the *next* step is often a `/learnings` entry.
- `state/quality/pr-grades/README.md` — explains the artifact directory + retention policy.
- `state/quality/pr-grades/SCHEMA.json` — JSON Schema for the grade card.
- `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md` — item #5; the parent plan.
- `state/harness/items.json` — item #5 status; bump from `todo` → `in_flight` → `shipped` as this skill lands.
