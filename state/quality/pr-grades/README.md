# state/quality/pr-grades — PR Grade Cards

Append-only ledger of **intent-vs-delivery grades** for merged PRs, produced
by the `/grade-last-pr` skill (see `.claude/skills/grade-last-pr/SKILL.md`).

Each file is one merged PR's grade card:

```
state/quality/pr-grades/
├── README.md          ← this file
├── SCHEMA.json        ← JSON Schema (pr-grade-v1)
└── <merge-sha>.json   ← one per merged PR; full 40-char SHA
```

## Why this exists

Closes item #5 of `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md`:
"Cybernetic governance loops". Without a post-merge evaluator, "an agent
declares done and that's the end of the loop." This directory is that
evaluator's output.

Karpathy R4 in one sentence: **task completed != goal achieved**. The grade
card asks, after the fact, whether the diff that landed actually delivers
what the PR title and Summary promised.

## Schema (pr-grade-v1)

| Field | Type | Notes |
|---|---|---|
| `schema` | string | Literal `"pr-grade-v1"`. |
| `pr_number` | integer | The PR number (e.g. `308`). |
| `merge_sha` | string | **Full 40-char** merge SHA. |
| `merged_at` | string | RFC3339 UTC. |
| `title` | string | The PR title from `gh pr view`. |
| `stated_intent` | string | PR title + the `## Summary` section of the body. |
| `actual_files_changed` | string[] | Output of `git show --name-only <sha>`. |
| `alignment` | enum | One of `"high"`, `"medium"`, `"low"`. See the SKILL for tie-breakers. |
| `reasons` | string[] | 1–5 short sentences explaining the alignment call. |
| `specialist_notes` | object | Map of specialist name → one-paragraph verdict (from `/octo:review`). |
| `codex_review` | object | Codex bot review scan result. `status` (`reviewed`/`not_reviewed`/`skipped`), `comments_total`, `unresolved` per priority, `findings[]`. P0/P1 degrade the grade — see SKILL step 5. |
| `graded_at` | string | RFC3339 UTC. When the grade was written. |
| `grader` | string | Model name (e.g. `"claude-opus-4-7"`). |
| `prior_grades` | object[] | Optional. Populated only on re-grades — see "Re-grading" below. |

Validate any grade card against `SCHEMA.json` before writing:

```bash
# If npx ajv is available:
npx -y ajv-cli@5 validate -s state/quality/pr-grades/SCHEMA.json \
  -d state/quality/pr-grades/<merge-sha>.json
```

## Alignment buckets at a glance

- **high** — diff matches stated intent; no surprises.
- **medium** — diff matches intent but introduces something not called out.
- **low** — diff drifts from intent (e.g., "fix typo" PR refactors 3 files).

The SKILL has worked examples for the tricky calls.

## Re-grading

Grade cards are append-mostly. If the same `<merge-sha>.json` is graded a
second time (e.g. by a different model, or after a follow-up `/octo:review`
shows something the first pass missed), the skill **must** preserve the
prior grade by moving it into `prior_grades[]`:

```json
{
  "schema": "pr-grade-v1",
  "pr_number": 308,
  "merge_sha": "abc123...",
  "alignment": "medium",
  "reasons": ["…revised reason…"],
  "graded_at": "2026-05-24T10:00:00Z",
  "grader": "claude-opus-4-7",
  "prior_grades": [
    {
      "alignment": "high",
      "reasons": ["…original reason…"],
      "graded_at": "2026-05-23T23:50:00Z",
      "grader": "claude-opus-4-7"
    }
  ]
}
```

Drift in grading itself is signal — if a PR moves from `high` to `low` two
weeks later because downstream regressions surfaced, that's exactly the
kind of trend this ledger exists to expose.

## What goes here vs elsewhere

| Question | File |
|---|---|
| "Did this merged PR deliver what its title said?" | `state/quality/pr-grades/<sha>.json` |
| "What's the trend of frontend type errors over time?" | `state/quality/embeddings/` and the parent `state/quality/` snapshots |
| "What surprised the agent this week?" | `docs/solutions/<category>/<date>-<topic>.md` (via `/learnings`) |
| "What's the agent's cursor right now?" | `state/digests/latest.md` (via `/digest`) |

## Retention policy

Keep all grade cards. Each is < 5 KB; even 10,000 merged PRs would be < 50 MB.
Do **not** prune. The histogram of `alignment` over time is a long-arc
quality signal.

## Related

- `.claude/skills/grade-last-pr/SKILL.md` — the producer.
- `SCHEMA.json` — JSON Schema for `pr-grade-v1`.
- `../README.md` — parent `state/quality/` directory contract.
- `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md` — plan item #5.
- `state/harness/items.json` — harness rollout tracker.
