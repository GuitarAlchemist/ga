# state/quality/test-plans — Test Plan Proposals

Append-only archive of **diff-driven test plan proposals** produced by the
`/test-plan` skill (see `.claude/skills/test-plan/SKILL.md`) and posted as
sticky PR comments by `.github/workflows/test-plan-suggester.yml`.

Each PR head SHA produces two files — a human-readable markdown proposal
and a structured JSON sidecar:

```
state/quality/test-plans/
├── README.md                 ← this file
├── SCHEMA.json               ← JSON Schema (test-plan-v1) for the sidecar
├── .gitkeep
├── <head-sha>.md             ← the proposal (markdown, what the PR comment shows)
└── <head-sha>.meta.json      ← structured metadata (sidecar — for trend analysis)
```

`<head-sha>` is the **full 40-char Git SHA** at proposal time, so files sort
chronologically and never collide on a short-SHA prefix. A new push to the
PR produces a new SHA and a new pair of files; the prior pair stays for
history.

## Why this exists

Closes the **"developer forgot to write a test plan"** gap. Today, every PR
template has a `## Test plan` checklist, but humans skip it ("I'll add tests
after merge"), agents under-fill it (token cap truncates), and reviewers
don't enforce it.

`/test-plan` shifts the test-design conversation **left** of merge by
emitting a concrete proposal at PR-open time — keyed to the actual diff,
not the whole module. The artifact is persisted so:

- The PR comment stays in sync with the proposal (sticky-comment upsert).
- A future `/grade-last-pr` invocation can read what was *proposed* vs what
  *landed* (closes the intent-vs-delivery loop on the test-coverage axis).
- We can graph proposal density per layer over time and detect when the
  heuristic starts over- or under-firing.

Pairs with `/grade-last-pr` (backward-looking) and `/council` (escalated
gate for one-way doors) to form a before / during / after triangle of PR
quality.

## Schema (sidecar JSON: test-plan-v1)

The `.meta.json` sidecar is the structured form. The `.md` is the prose
form that appears in the PR comment and the artifact directory.

| Field | Type | Notes |
|---|---|---|
| `schema` | string | Literal `"test-plan-v1"`. |
| `pr_number` | integer | The PR number. |
| `head_sha` | string | Full 40-char head commit SHA. |
| `risk_class` | enum | One of `pure-additive`, `refactor`, `api-change`, `one-way-door`. |
| `proposal_counts` | object | `{ unit, integration, e2e, chatbot, coverage_gaps }`. All integers ≥ 0. |
| `layers_touched` | string[] | Layer slugs (e.g. `dotnet-domain`, `react-spa`, `vite-middleware`). |
| `generated_at` | string | RFC3339 UTC. When the proposal was written. |
| `generator` | string | Model name (e.g. `claude-opus-4-7`). |
| `suppressed` | bool | Optional. `true` when author put `[skip test-plan]` in the body. |
| `author_is_agent` | bool | Optional. `true` when commit trailers include `Co-Authored-By: <agent>`. |
| `body_has_test_plan_section` | bool | Optional. `true` when PR body already has `## Test plan`. |

Validate against `SCHEMA.json`:

```bash
# If npx ajv is available:
npx -y ajv-cli@5 validate -s state/quality/test-plans/SCHEMA.json \
  -d state/quality/test-plans/<head-sha>.meta.json
```

## Markdown artifact layout

Section order matters — the auto-fire workflow's sticky-comment poster may
truncate at the `## Coverage gaps surfaced` header if the proposal exceeds
GitHub's 65 KB comment limit on overgrown PRs.

```
# Test plan — PR #N (<short-sha>)

**Generated:** ...
**Author:** @gh-login
**Scope:** ...
**Risk class:** ...

## Unit tests (N proposed)
## Integration tests (N proposed)
## E2E tests (Playwright) (N proposed)
## Chatbot prompts (N proposed)
## Coverage gaps surfaced
## Rubric
```

The full template lives in `.claude/skills/test-plan/SKILL.md` §4 and is the
source of truth.

## What goes here vs elsewhere

| Question | File |
|---|---|
| "What tests should this open PR add before merge?" | `state/quality/test-plans/<head-sha>.md` |
| "Did the merged PR deliver what its title said?" | `state/quality/pr-grades/<merge-sha>.json` (via `/grade-last-pr`) |
| "Did this one-way-door PR pass the council gate?" | `state/quality/council/<head-sha>.json` (via `/council`) |
| "What is the QA trend across the codebase?" | `state/quality/dashboard-playwright/` etc. |
| "What new tests actually got written?" | the PR diff itself + the next `/grade-last-pr` run |

## Retention policy

Keep all proposals indefinitely. Each pair (`.md` + `.meta.json`) is
typically 4–15 KB; even 10,000 PRs is < 150 MB. Do **not** prune.

The trend signal in `proposal_counts` over time is what tells us whether
the heuristic in the SKILL needs tuning — drift in `unit:integration:e2e`
ratios is a quality-of-process signal worth keeping.

## Re-proposing

Force-pushes that change `head_sha` produce a new artifact pair; the prior
pair stays for audit history. This mirrors `/council`'s convention (verdicts
pin to the commit they judged) and is intentional — the proposal at
SHA A might differ materially from the proposal at SHA B after a force-push.

If `/test-plan` is invoked twice on the **same** `head_sha` (e.g. by a
human after the auto-fire workflow ran), the second run **overwrites** the
pair — no `prior_proposals[]` history. The proposal is a snapshot of the
heuristic at a moment in time; it's not graded prose like `/grade-last-pr`.

## Related

- `.claude/skills/test-plan/SKILL.md` — the producer; full heuristic in §3.
- `.github/workflows/test-plan-suggester.yml` — auto-fire workflow that
  posts the sticky comment on PR `opened` + `synchronize`.
- `SCHEMA.json` — JSON Schema for the sidecar (`test-plan-v1`).
- `.claude/skills/grade-last-pr/SKILL.md` — the backward-looking sibling.
- `.claude/skills/council/SKILL.md` — the escalated-gate sibling.
- `../README.md` — parent `state/quality/` directory contract.
- `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md` — the
  parent plan that motivates the cybernetic-loop family.
