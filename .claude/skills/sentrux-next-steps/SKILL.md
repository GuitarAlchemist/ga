---
name: sentrux-next-steps
description: Turns raw Sentrux structural-quality metrics (quality_signal, cycle count, coverage) into a ranked list of actionable refactor recommendations with starter sketches. Pulls live data from sentrux MCP tools, optionally cross-references AI annotations (@ai:business-value, @ai:smell) and the latest /test-plan output, then writes 5-10 prescriptive bullets to state/quality/sentrux-next-steps/<date-Z>.md so the Sentrux dashboard tab can render them. PROPOSES — never auto-refactors; the human picks which recommendation to take.
allowed-tools: Bash, Read, Write, Grep, Glob, mcp__sentrux__check_rules, mcp__sentrux__dsm, mcp__sentrux__test_gaps, mcp__sentrux__git_stats, mcp__sentrux__scan, mcp__sentrux__health
last_verified: 2026-05-24
karpathy_rule: R4-goal-driven-execution (raw metrics aren't goals — recommendations are)
related_plan: docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md
---

# /sentrux-next-steps

Closes the **"raw metrics, no prescription"** gap on the Sentrux dashboard
tab. Today operators see `quality_signal=3015`, `632 cycles`, `6.8%
coverage` and a bottleneck label — useful but not directive. This skill
converts those numbers into a ranked list of refactor + coverage moves with
starter sketches, persists them to `state/quality/sentrux-next-steps/`,
and the `SentruxNextStepsCard` component on `/test#dev/sentrux` renders
the result at the top of the tab.

Invoked as `/sentrux-next-steps` from any Claude Code session, or queued
via the **Regenerate** button on the Sentrux Next Steps card (which POSTs
to `/actions/harness/skill/sentrux-next-steps` — an agent picks it up).

Sister skills:

- `/test-plan` — diff-driven test proposals for an open PR (forward-looking).
- `/grade-last-pr` — intent-vs-delivery on the merged PR (backward-looking).
- `/sentrux-next-steps` — structural-quality refactor proposals (this skill,
  ambient — fires on demand or daily).

Together they answer:
- `/test-plan` — *what should I test on this PR?*
- `/grade-last-pr` — *did the last PR deliver what its title said?*
- `/sentrux-next-steps` — *what should I refactor next, period?*

## When to run

- **When the Sentrux Next Steps card is empty** or stale (last generation
  > 7 days). The card surfaces last-generated timestamp; operators hit
  Regenerate when the data feels old.
- **After a large structural change merges** — the cycle count and
  quality_signal shift; the prior recommendations may be moot. Re-run to
  refresh the prescription.
- **At the start of a refactor sprint** — gives the team a written,
  ranked menu instead of "let's grep for cycles and see".
- **Daily via workflow** (future) — the same skill can fire on a cron
  to keep `latest.md` fresh without operator action. Today the skill
  is manual-only.

**Do NOT** invoke for:

- A repo with no sentrux scan history — the recommendations would be
  speculation. Run `mcp__sentrux__scan` first and verify `health` returns
  a number.
- Hot-fix branches — this is a strategic prescription, not a debugging
  aid. Use `/octo:debug` for "why is this broken right now?".
- Pure docs/CI sprints — sentrux measures code structure; recommending
  cycle-breaks on a documentation-only push is noise.

## What this skill does NOT do

- **Never auto-refactors code.** The prescription is prose + file paths,
  not patches. Refactoring requires intent, fixture awareness, and the
  team's style — that's an engineering call.
- **Never opens a PR.** The artifact is markdown only; the operator (or
  a follow-on agent) writes the PR with the recommendation as input.
- **Never grades past recommendations.** Whether the team took a
  recommendation is observable from `git log` + the next sentrux run
  (did `quality_signal` move?). A future `/grade-sentrux-next-steps`
  could close that loop; this skill stops at the proposal.
- **Never edits the dashboard.** Writing to `state/quality/sentrux-next-steps/`
  is enough — the SentruxNextStepsCard auto-renders via
  `/dev-data/sentrux/next-steps`.

## How to run

### 1. Pull current sentrux state

Hit the MCP tools (preferred) or the dashboard endpoints (fallback when
running outside an MCP-aware session):

```
mcp__sentrux__scan         # ensure fresh
mcp__sentrux__health       # quality_signal, bottleneck, root_causes
mcp__sentrux__check_rules  # current violations
mcp__sentrux__dsm          # cycles, hotspots
mcp__sentrux__test_gaps    # untested files (riskiest first)
mcp__sentrux__git_stats    # recent churn (hotspots ≠ refactor priority)
```

Endpoint fallback (curl against the running Vite dev server, port 5176):

```bash
curl -s http://localhost:5176/dev-data/sentrux/health    > /tmp/sx-health.json
curl -s http://localhost:5176/dev-data/sentrux/rules     > /tmp/sx-rules.json
curl -s http://localhost:5176/dev-data/sentrux/dsm       > /tmp/sx-dsm.json
curl -s http://localhost:5176/dev-data/sentrux/test-gaps > /tmp/sx-gaps.json
```

If sentrux is unreachable (sentrux.exe missing, no Vite running), abort
with a clear error rather than synthesizing recommendations from nothing.
The skill's output must be grounded in real measurements.

### 2. Pull optional cross-references

These improve ranking but the skill works without them.

**AI annotations** — `@ai:business-value` says which files matter most;
`@ai:smell` overlaps with sentrux structural findings. Skip if empty:

```bash
curl -s http://localhost:5176/dev-data/ai-annotations > /tmp/sx-ann.json
```

A file with both `business-value: high` and a sentrux cycle/hotspot
membership gets ranked higher than an equal-structural-cost cycle in a
low-value module.

**Latest test plan** — `state/quality/test-plans/*.md` (most recent by
mtime) gives coverage context: if the test plan already proposes tests
for file X, don't re-recommend "cover file X" in this skill.

```bash
ls -t state/quality/test-plans/*.md 2>/dev/null | head -1
```

### 3. Synthesize 5-10 ranked recommendations

This is the load-bearing prose step. Rank by **leverage** — how much the
recommendation moves `quality_signal` per unit of effort, weighted by
business value.

Each recommendation has six required parts:

| Part | Length | Source of truth |
|---|---|---|
| **Title** | one short imperative sentence | "Break the X ↔ Y cycle" |
| **Impact** | quantified | "removes 23 backward edges, +3% quality_signal" |
| **Effort** | t-shirt + 1 sentence rationale | "M — ~2 days, touches 5 files" |
| **Where** | file paths + line ranges | exact `<repo>/<path>:<line-range>` |
| **Starter sketch** | 3-5 sentences of refactor direction (NOT code) | extract interface, move impl, verify with ix-graph |
| **Why now** | risk/value tie-in | "high-value module per `@ai:business-value`" |

Rules of thumb for ranking:

| Signal | Bias |
|---|---|
| Cycle members include any `@ai:business-value: high` file | +1 priority |
| Cycle includes a controller (`Apps/*/Controllers/`) | +1 (every request crosses it) |
| File in cycle has high `git_stats` churn | +1 (refactor pays off across PRs) |
| File in cycle is untested per `test_gaps` | +1 (refactor without tests is risky → propose coverage FIRST) |
| Bottleneck is `acyclicity` | rank cycle-breaks above coverage |
| Bottleneck is `coverage` | rank test additions above refactors |
| Bottleneck is `redundancy` | rank deduplication above either |

Cap the list at **10**. If you have more, the prescription is unfocused —
pick the top 10 by leverage and note `+N more candidates not listed` at
the bottom. Operators skip 20-item lists.

### 4. Write the artifact

```bash
DATE_Z=$(date -u +%Y-%m-%dT%H-%M-%SZ)
OUT="state/quality/sentrux-next-steps/${DATE_Z}.md"
```

Frontmatter template (REQUIRED — the middleware reads it):

```markdown
---
schema: sentrux-next-steps-v1
generated_at: <RFC3339 UTC>
generator: claude-opus-4-7
inputs:
  quality_signal: <number>
  cycles: <number>
  coverage_pct: <number>
  bottleneck: <string>
  value_annotations: <count of @ai:business-value annotations seen>
  smell_annotations: <count of @ai:smell annotations seen>
---

# Sentrux Next Steps — <date-Z>

> Quality signal: <N> / 10,000 · <N> cycles · <X>% coverage
> Bottleneck: <name> · Top root cause: <name> (<score>)

## 1. <Imperative title>

**Impact:** <quantified>
**Effort:** <S/M/L> — <one-sentence rationale>
**Where:** `<paths>`
**Starter:** <3-5 sentences of direction, NOT code>
**Why now:** <value/risk tie-in>

## 2. ...

...

## Inputs

This recommendation set was generated from:
- sentrux health @ <RFC3339>
- N rule violations
- N cycles in DSM
- N untested files
- N AI annotations (business-value / smell)
- Latest test plan: `state/quality/test-plans/<sha>.md` (or "none")
```

### 5. Maintain `latest.md`

The middleware reads `latest.md` (not the dated file). Update it after
writing the dated artifact:

```bash
# Windows-friendly: copy, don't symlink. Symlinks need admin / dev mode.
cp "$OUT" state/quality/sentrux-next-steps/latest.md
```

If you have multiple writers in flight, copy is safe; the next write
overwrites cleanly. Don't try to atomic-rename across drives.

### 6. Print the terminal summary

Keep it under 10 lines so the calling agent's report stays compact:

```
sentrux-next-steps · quality 3015 → bottleneck: acyclicity
  inputs:  632 cycles · 6.8% coverage · 12 violations · 8 high-value annotations
  emitted: 7 recommendations (3 refactor, 2 coverage, 1 dedup, 1 dependency split)
  top pick: "Break the Music.Theory ↔ Apps.GaApi cycle" (+3% signal, M effort)
  artifact: state/quality/sentrux-next-steps/2026-05-24T16-22-08Z.md
  latest:   state/quality/sentrux-next-steps/latest.md
```

## Edge cases

- **Sentrux returns `quality_signal: null` or errors.** Abort. Write a
  short note to the dated file: `# Sentrux unreachable at <ts> — no
  recommendations possible`. Don't fall back to "generic refactor advice"
  — the value of the skill is that it's grounded in measurement.
- **No cycles at all.** Congratulations. Emit 3-5 coverage / dedup
  recommendations instead. Note "No structural cycles detected; focus
  shifts to coverage." at the top.
- **The same recommendation appeared last run and is still pending.**
  Include it — the operator clearly hasn't gotten to it. Add a `(carried
  over from <prior date>)` suffix to the title.
- **AI annotations endpoint returns `{empty: true}`.** Drop the
  business-value weighting; rank purely by structural leverage. Note in
  the `## Inputs` section that the rank ignores business value.
- **Test gaps payload is the free-tier aggregate shape** (no per-file
  `files[]`). Drop coverage-specific recommendations; you can't name
  the riskiest untested file. Note in `## Inputs`.
- **The skill is invoked but the dashboard isn't running.** The artifact
  is still useful — operators can read `latest.md` from disk. Don't gate
  on dashboard availability.

## Anti-patterns

- **"Refactor everything in `Music.Theory`."** Too broad. Always name
  the cycle, the files in it, and the boundary you're proposing.
- **"Add tests for `FooService`."** Empty without naming the
  acceptance behavior. Cite the line range + the public method whose
  contract the test would lock in.
- **Recommending code that the skill itself generates.** This skill
  PROPOSES; the operator writes. If you find yourself writing
  ```csharp
  public interface IChordEngine { ... }
  ```
  inside the artifact, stop — that's a future `/refactor-sketch` skill,
  not this one.
- **Posting more than 10 items.** Operators skip the list; the
  prescription becomes noise.
- **Re-using stale numbers.** If `quality_signal` changed > 5% since the
  last artifact, the prior prescription's "Impact" claims may now be
  wrong. Re-pull `health` immediately before writing.
- **Mixing tactical (this-PR) and strategic (this-quarter) advice.**
  This skill is strategic — multi-day, structural moves. Tactical
  PR-scoped suggestions belong in `/test-plan` or PR review comments.

## Heuristic tuning notes

The ranking signals in §3 are the leverage points. To tune:

1. After a recommendation is taken, re-run the skill and check whether
   the next prescription cites *its consequence* (e.g. "now that the X↔Y
   cycle is broken, the Z↔W cycle is the largest"). If yes, the ranking
   is working. If no, the signals need rebalancing.
2. If the dashboard's `quality_signal` moves but the next prescription
   doesn't acknowledge the move, the skill is failing to read its own
   prior artifact. The `## Inputs` block exists to make this checkable.

## Why this exists

Karpathy R4: "Task completed != goal achieved." A `quality_signal` of
3015 is a measurement, not a goal. The goal is "what change moves us
toward 5000." This skill writes that change down.

Today, the Sentrux tab shows the measurement. Operators internally
translate "632 cycles, bottleneck=acyclicity" into "I should break some
cycles" — but which ones? Where? In what order? With what starter? This
skill makes that translation visible, ranked, and persistent so the next
session (or the next teammate) starts from the prescription, not from
the raw numbers.

The skill is read-only on the codebase (writes only to
`state/quality/sentrux-next-steps/`). The artifact is a starter — the
operator owns the actual refactor PR.

## Related

- `state/quality/sentrux-next-steps/README.md` — artifact directory
  contract (this skill's output).
- `ReactComponents/ga-react-components/src/components/Sentrux/SentruxNextStepsCard.tsx`
  — the consumer (dashboard surface).
- `ReactComponents/ga-react-components/vite.config.ts` — the
  `/dev-data/sentrux/next-steps` middleware that reads `latest.md`.
- `.claude/skills/test-plan/SKILL.md` — sibling forward-looking skill.
- `.claude/skills/grade-last-pr/SKILL.md` — sibling backward-looking
  skill.
- `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md` —
  parent plan; the cybernetic-loop family this skill belongs to.
