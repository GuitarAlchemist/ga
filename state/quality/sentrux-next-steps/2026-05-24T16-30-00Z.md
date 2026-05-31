---
schema: sentrux-next-steps-v1
generated_at: 2026-05-24T16:30:00Z
generator: hand-seed-2026-05-24
inputs:
  quality_signal: 3015
  cycles: 632
  coverage_pct: 6.8
  bottleneck: acyclicity
  value_annotations: 0
  smell_annotations: 0
---

# Sentrux Next Steps — 2026-05-24

> Quality signal: 3,015 / 10,000 · 632 cycles · 6.8% coverage
> Bottleneck: acyclicity · Top root cause: redundancy (4,948)

These are **seed recommendations** hand-authored from the dashboard
screenshot at PR open. They are plausible starting prescriptions, not
authoritative — re-run `/sentrux-next-steps` once the live skill is
wired so an agent regenerates them grounded in real sentrux DSM /
test-gap output.

## 1. Break the largest cross-module cycle (REFACTOR FIRST)

**Impact:** Estimated removal of ~30-50 backward edges → quality_signal
projected +2 to +3% (acyclicity is the bottleneck dimension; moves the
needle most per file touched).
**Effort:** M — ~2 days. Single interface extraction + one project
reference flip; tests likely still pass without rewrites.
**Where:** Cycles involving `Common/GA.Business.Core/Music.Theory/**`
and `Apps/ga-server/GaApi/Controllers/**` (run `mcp__sentrux__dsm` to
get the exact cycle membership; the dashboard's DSM card shows the
matrix).
**Starter:** Extract a `IChordEngine` (or similar boundary) interface
in `Common`, have Controllers depend on the interface only, move the
concrete implementation to a leaf project that depends on Common.
Verify the cycle is gone by re-running `mcp__sentrux__dsm` and the
cycle count drops. Bonus: this is the model for every subsequent
cycle break — pick the largest first because the pattern compounds.
**Why now:** acyclicity is the named bottleneck; cycle-breaks have the
highest leverage right now. With 632 cycles, even a 10% reduction is
~60 fewer edges.

## 2. Deduplicate the top redundancy hotspots (HIGHEST ROOT CAUSE)

**Impact:** redundancy is currently the dominant root cause at 4,948.
Even modest dedup (5-10% by line count) moves quality_signal noticeably.
**Effort:** M — ~1-3 days depending on how aggressive. Mostly mechanical
once the duplicates are named.
**Where:** Run `mcp__sentrux__check_rules` with the duplication-detector
rules enabled; the top offenders are typically copy-paste families in
the recognizer + voicing code paths.
**Starter:** Identify the top 5 duplicate blocks (look for clusters of
>30 LOC repeated 3+ times). Extract each cluster into a single private
helper in its nearest common parent. Don't try to dedup across modules
yet — that interacts with cycle-breaking and should come after item #1.
**Why now:** redundancy ≈ 1.6× the next-largest root cause. The signal
is loud; the rules engine already names the locations.

## 3. Cover the top 5 untested critical-path files (KEEP STABLE)

**Impact:** Doesn't directly move quality_signal much (coverage is the
secondary bottleneck), but creates the safety net required to refactor
items #1-2 without regression risk.
**Effort:** S/M per file — 1-2 unit tests + one happy-path integration
test per file. ~1 day total for 5 files.
**Where:** Run `mcp__sentrux__test_gaps --limit 20`; pick the 5 files
with the highest `risk_score` that ALSO appear in any of the cycles
from item #1 (refactoring untested code is risky; cover first).
**Starter:** One test per public method, asserting the documented
behavior. Don't aim for branch coverage on the first pass — line
coverage is fine. Use the existing xUnit/Expecto/Vitest patterns in
the sibling folders; do NOT introduce a new test framework.
**Why now:** 6.8% coverage is precariously low for a structural
refactor sprint. Covering the critical path first makes items #1-2
safe.

## 4. Triage existing rule violations (CHEAP WIN)

**Impact:** Each fixed violation is a direct +1 in the rules score
component of quality_signal. If there are 12 violations and you close
5, that's a measurable bump.
**Effort:** S — most rule violations are local fixes (missing override,
inconsistent naming, accidental import). Couple of hours total.
**Where:** Sentrux Rule Violations card on the dashboard, or
`mcp__sentrux__check_rules`.
**Starter:** Sort violations by severity (errors first, then warnings).
Group by file. Fix the file with the most violations first to clear
the most ground per code-review. Skip any violation that requires
architectural change — those belong in items #1-2.
**Why now:** the rules card is already populated; this is the lowest
friction win on the board.

## 5. Add `@ai:business-value` annotations to the top 20 files
*(UNLOCKS BETTER FUTURE RANKING)*

**Impact:** Doesn't move quality_signal at all. But the next run of
`/sentrux-next-steps` ranks cycles by business value × structural cost
— and right now the ranker has zero business-value data, so it's
running on structural cost only. Annotating the top 20 files
(controllers, recognizers, OPTIC-K hotspots) unlocks better prescriptions
on the next run.
**Effort:** S — 20 files × one one-line annotation each = ~30 minutes.
**Where:** Walk `Apps/ga-server/GaApi/Controllers/*.cs`,
`Common/GA.Business.Core/Music.Theory/Recognizers/*.cs`, and the
OPTIC-K search path; add `// @ai:business-value: high|medium|low` near
the class declaration.
**Starter:** Use `high` for anything in the request path (controllers,
recognizer entry points, voicing search). `medium` for shared helpers
(theory math, formatters). `low` for test scaffolds and one-off scripts.
Don't overthink it — coarse-grained annotations are fine; the ranker
just needs a tiebreaker signal.
**Why now:** the next prescription compounds. Investing 30 minutes once
makes every future `/sentrux-next-steps` run measurably more useful.

## 6. Snapshot the DSM and lock in a "no new cycles" gate

**Impact:** Prevents regression of items #1-2. Without a gate, the
cycle count climbs back up between refactor PRs as the team adds new
imports.
**Effort:** S — ~2 hours. The gate is a small CI workflow that calls
`mcp__sentrux__dsm`, compares cycle count to a baseline in
`state/quality/`, and fails if it grew by more than 5.
**Where:** New file `.github/workflows/sentrux-cycle-gate.yml`; baseline
file `state/quality/sentrux-baseline.json`.
**Starter:** Take today's cycle count (632) as the baseline; allow a
+5 tolerance for routine churn. When the gate fires red, the PR author
sees the new edge and decides: add a comment justifying it, or refactor.
This is the regression-catching twin of items #1-2 — together they form
a ratchet (cycle counts can go down but not up by more than the
tolerance).
**Why now:** without a gate, the refactor work from items #1-2 silently
unwinds.

## 7. Wire `/sentrux-next-steps` to auto-fire daily

**Impact:** Operational — keeps the prescription fresh without operator
action. Without auto-fire, the prescription staleness is the
operator's mental burden.
**Effort:** S — ~1 hour. Reuse `.github/workflows/test-plan-suggester.yml`
as a template; trigger on `schedule: cron: '0 13 * * *'` (08:00 ET
daily, after the European day-shift slowdown).
**Where:** New workflow `.github/workflows/sentrux-next-steps-daily.yml`.
**Starter:** Workflow runs on schedule. Step 1: invoke
`/sentrux-next-steps` (queue via the existing skill-invocation file).
Step 2: an agent picks the queue entry up on the next cycle and writes
the artifact. Step 3 (optional): if the prescription changes
materially (top-3 reordered), open a tracking issue.
**Why now:** this skill is on-demand only today. Daily cadence matches
the dashboard polling cadence and means the prescription never goes
more than 24h stale.

## Inputs

This prescription was assembled from the dashboard screenshot at PR
open. Counts and bottleneck label come from the live `SentruxHealthCard`
render at the time of writing:

- sentrux health: quality_signal=3015, bottleneck=acyclicity
- root causes: redundancy=4948 (dominant), other causes not transcribed
  from the screenshot
- DSM cycle count: 632 (from the bottleneck rationale)
- test_gaps coverage: 6.8% (from the rationale)
- rule violations: count not yet transcribed (see Rules card)
- AI annotations: none yet — item #5 above unlocks this for future runs
- prior test plans consulted: `state/quality/test-plans/2026-05-24-gachatbot-baseline.md`
  (chatbot-only scope; no overlap with the refactor recommendations
  above)

**Next regeneration:** click **Regenerate** on the Sentrux Next Steps
card, or wait for the (future) daily workflow.
