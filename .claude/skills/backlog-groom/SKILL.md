---
name: backlog-groom
description: Backlog grooming. Reads BACKLOG.md (H2 epics → H3 sub-sections), recent open GitHub issues, state/quality/ trend snapshots, state/digests/latest.md, and stale docs/plans/, then proposes a ranked top-3 work items with rationale. Writes the same content to state/backlog-grooming/<date>.md so the proposal survives session compaction. Closes the "agent picks whatever's loudest" failure mode in autonomous mode. PROPOSES — never auto-executes; the human approves.
allowed-tools: Bash, Read, Write, Glob, Grep
last_verified: 2026-05-24
karpathy_rule: R4-goal-driven-execution (every proposed item declares verifiable success criteria)
related_plan: docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md
---

# /backlog-groom

Produces a ranked, justified **top-3 next work items** for the human to
approve. Pairs with `/digest` (state cursor) and `/grade-last-pr`
(post-merge evaluator) to close the planning loop: digest captures the
*now*, grade-last-pr captures *what just shipped*, backlog-groom proposes
*what next*.

This is a **Phase 2 harness agent** (#3 of 3). Siblings:
`semantic-regression-agent` (chatbot-qa drift) and `token-spend-agent`
(AI-cost drift). No file overlap with either.

## When to run

- **Once per week** — Monday morning is the canonical slot. Workflow
  `.github/workflows/weekly-backlog-grooming.yml` runs every Monday at
  8:57 local (off-:00 mark to dodge cron stampedes) and posts the
  proposal as a comment on the long-lived `[meta] Backlog grooming
  tracker` GitHub issue, tagging @spareilleux for go/no-go.
- **After a major milestone ships** — a big PR merge means the cursor
  moved; the next-3 from before may now be stale.
- **At session start when "what should I work on?" is unclear** —
  cheaper than re-deriving priorities from scratch each session.

**Do NOT** invoke:

- On every session (noise — the prior week's proposal still applies).
- For trivial-scope decisions (a single typo fix doesn't need ranking).
- As a substitute for `/digest` (different artifact — state vs proposal).

## Why this exists

The current failure mode in autonomous mode is **"agent picks whatever's
loudest"**: whichever GitHub issue was opened last, whichever test
failure flashed most recently, whichever Slack-message-shaped artifact
was most legible. No agent reads the *full* signal stack (backlog +
issues + quality trends + in-flight cursor + stale plans) and emits a
*justified* ranking. This skill is that agent.

Backlog grooming for autonomous work is a **one-way door** (wrong
priorities compound — each wrong week eats a week of opportunity cost),
so the skill PROPOSES; the human approves. Even the weekly cron posts
a *comment*, not a dispatch.

## Scoring rubric (opinionated, defensible — override at will)

Every candidate gets four scores. Document any override in the proposal.

### Impact — H / M / L

- **H** — moves a tracked baseline metric (frontend typecheck error
  count, chatbot-qa pass-pct, post-merge-smoke pass rate, invariant
  coverage), closes a security/data-loss gap, or unblocks ≥2 other
  items.
- **M** — improves dev-experience or unblocks 1 item, doesn't move a
  tracked metric directly.
- **L** — cosmetic, exploratory, or pure-learning. Default for "I had
  an idea last night."

### Effort — S / M / L

Agent-time + human-review-time required.

- **S** — under a half-day; one file or one workflow; no API surface
  change; no schema change.
- **M** — half-day to two days; multiple files; possibly a small
  schema bump; PR needs a real review.
- **L** — multi-day; cross-repo coordination; one-way-door decision;
  council-style review needed.

### Blast radius — pure-additive / two-way door / one-way door

- **pure-additive** — new file under `.claude/`, new doc, new workflow
  that doesn't touch existing config. Safe to ship without ceremony.
- **two-way door** — modifies existing config, refactors code, changes
  a default. Reversible by `git revert`.
- **one-way door** — schema hash change, public crate/API surface,
  OPTIC-K partition layout, Galactic Protocol contract, deletes data,
  rewrites history. Needs the `/council` gate (item #6 of harness
  plan) or explicit human sign-off.

### Recency — fresh / stale / dormant

- **fresh** — touched in the last 7 days (recent digest, recent commit,
  recent issue activity). Lower priority for *new* work — momentum is
  on it already.
- **stale** — last touched 7–30 days ago. Higher priority for *now* —
  context is still warm, will go cold soon.
- **dormant** — 30+ days. Either revive deliberately or close. Don't
  silently leave dormant; surface it for a kill/keep decision.

### Tie-breaker order (when scores collide)

1. **Higher impact** wins.
2. **Lower blast radius** wins (additive beats two-way beats one-way).
3. **Staler** wins (recency: stale beats fresh — context-warm, will go
   cold).
4. **Lower effort** wins.

The first three are the load-bearing rules. Effort is the last
tie-breaker because "easiest" is the seductive wrong default — it's
how autonomous mode degenerates into completion-bias drift.

## How to run

### 1. Read BACKLOG.md (H2 epics → H3 sub-sections)

The parser at
`ReactComponents/ga-react-components/src/dev-data/parsers.ts`
(`parseBacklog`) is the canonical structure. Mirror it:

- `## Epic name` → epic
- `### Sub-section name` → categorized `shipped` / `active` / `backlog`
- `- bullet` → counted into the current sub-section
- Bullets under an H2 (before any H3) inherit the H2 category

Pull every bullet under `Active Ideas` or `backlog` sub-sections plus
anything under `P0` / `P1` / `P2` sub-sections in the chatbot/agent
sections. Skip everything under `Shipped` / `Parked`.

### 2. Read recent open GitHub issues

```bash
gh issue list --state open --limit 50 \
  --json number,title,labels,createdAt,updatedAt,author
```

Filter:

- Drop `[meta]` issues (tracker issues, not work items).
- Drop issues authored by bots (`-author dependabot` etc.).
- Keep label-prefixed issues as separate candidates only if they're
  not already mirrored in BACKLOG.md (cross-check by title token
  overlap).

### 3. Read quality-trend snapshots

```bash
# Latest typecheck baseline (item #2 / PR #301)
ls state/quality/dashboard-playwright/ 2>/dev/null | tail -5
# Chatbot QA trend (semantic-regression-agent feeds this)
ls state/quality/chatbot-qa/*.json | tail -5
# Post-merge smoke (item #4)
ls state/quality/e2e/*.json 2>/dev/null | tail -5
# Invariant coverage trend
ls state/quality/invariants/*.json 2>/dev/null | tail -5
```

For each, read the latest 2–3 and compute the delta. Flag anything
that's:

- **Regressing** — pass-pct dropped, error count rose, coverage fell.
- **Stuck** — no movement in 14+ days on a known-poor baseline.
- **Newly green** — was failing, now passing → candidate for victory
  lap / lock-in test.

Each anomaly becomes a candidate work item with `Impact: H` (it's
moving a baseline, by definition).

### 4. Read state/digests/latest.md

```bash
cat state/digests/latest.md 2>/dev/null
```

Pull the `Next action`, `In-flight`, and `Open questions` sections.
Anything `in-progress` or `pending` in the `success_criteria` frontmatter
is a high-recency candidate — finishing in-flight work beats starting
new work, usually.

### 5. Read stale docs/plans/

```bash
ls -t docs/plans/*.md | head -30
# For each: when was it last touched? (mtime ≈ last edit)
git log -1 --format='%ai %s' -- docs/plans/<file>
```

A plan untouched for 14+ days is one of:

- **Stale-revive** — still relevant, just dropped. Promote to next-3.
- **Stale-close** — superseded by what actually shipped. Propose
  closing (move under `## Parked` or delete).

Surface both kinds. The skill recommends; the human decides.

### 6. Score and rank

For each candidate, write a one-line scorecard:

```
<title>  |  Impact=H/M/L  Effort=S/M/L  Blast=add/2way/1way  Recency=fresh/stale/dormant
```

Apply the tie-breaker order. Take the top 3.

### 7. Write the proposal

To **both**:

- The session (markdown output to the user/transcript).
- `state/backlog-grooming/<YYYY-MM-DD>.md` (so it survives compaction).

Use the template at
`state/backlog-grooming/README.md` (the "Template" section).

For each of the top 3, the proposal MUST include:

- **Why now** — one sentence tying back to the rubric. Cite the metric
  / digest cursor / issue number / plan path that triggered it.
- **First slice** — the smallest PR-shaped chunk. If it doesn't fit
  in one PR, slice harder. "First slice" is the contract with the
  next agent.
- **Open questions** — what would block a session from starting
  immediately. Aim for zero; one is acceptable; two means it's not
  ready for next-3.

Optional but useful:

- **Anti-pattern check** — flag if the item is a candidate for the
  Sentinel's Void (governance over nothing — see CLAUDE.md / IX
  memory), completion bias (80%+small=done framing), or the
  "fix everything" creep.

### 8. Don't auto-execute

The skill ends after writing the proposal. Even when invoked from the
weekly cron, the cron only posts a *comment* on the tracker issue —
no auto-dispatch, no PR creation, no branch creation. Backlog
grooming for autonomous work is a one-way door (wrong priorities
compound), so council-style human gate.

## Output template

The proposal MUST follow this template (also in
`state/backlog-grooming/README.md`):

```markdown
# Backlog grooming — proposed next 3 (<YYYY-MM-DD>)

**Signal stack read:**
- BACKLOG.md: <N> epics, <M> active bullets
- Open issues: <N> (<filtered>)
- Quality trends: <one-line summary per category>
- Digest cursor: <Next action from state/digests/latest.md, or "no recent digest">
- Stale plans (14+ days): <count> (<filenames or "none">)

---

## 1. <Title> (Effort=<S/M/L>, Impact=<H/M/L>, Blast=<add/2way/1way>)

- **Why now:** <one sentence with metric/digest/issue/plan citation>
- **First slice:** <smallest PR-shaped chunk; one file or one workflow when possible>
- **Open questions:** <none / one acceptable question / "not ready" if 2+>
- **Anti-pattern check:** <none / Sentinel's Void / completion bias / scope creep>

## 2. <Title> ...

## 3. <Title> ...

---

## Honorable mentions (4–6)

Short rationale per item — these are the next candidates if any of the
top-3 gets vetoed by the human. Capped at 3 to avoid bloat; if you
have >3, the rubric needs sharpening.

---

## Kill / keep decisions surfaced

- **Dormant plan:** `docs/plans/<file>.md` (last touched <date>) —
  recommend <KILL / REVIVE> because <one sentence>.
- (repeat per dormant plan)

---

## Rubric overrides applied

If you deviated from the default scoring (e.g. demoted a high-impact
item because it's a one-way door without a council), document why
here so the next grooming can audit the call.
```

## Weekly digest mode

When invoked as `/backlog-groom --digest` OR when
`.github/workflows/weekly-backlog-grooming.yml` runs its Monday cron,
the skill produces a **"Last 7 days" trend digest** *in addition to*
the top-3 work-item proposal. The two artifacts are concatenated into
a single output (top-3 first, digest after the `---`) so one comment
on the tracker issue carries both.

The digest is the **trend-level supervisor complement** to the
algedonic-channel real-time incident layer (`state/algedonic/`):

- Algedonic = "what broke right now, who acks it" (seconds → hours).
- Weekly digest = "what shipped, what regressed, what's stuck" (days
  → weeks). The supervisor doesn't ack pings; it spots drift.

### Digest sections

Each section is a small markdown block. Empty sections render as
`_None this week._` rather than being silently dropped — the reader
needs to know the question was asked and answered "nothing".

1. **Shipped PRs (last 7d)** — table with columns `#`, title (truncated
   to 60 chars), merge SHA (short, 7 chars), shipper (parsed from the
   `Co-Authored-By:` trailer on the merge commit; falls back to PR
   author), and category. Category is a heuristic on title prefix:
   `feat(frontend|tonal-orbit|test-page)` → frontend; `feat(chatbot|
   semantic|llm)` → chatbot; `feat(harness|skill)` → harness;
   `fix(...)` → fix; `chore|docs|ci` → chore.
2. **Algedonic signals (last 7d)** — read `state/algedonic/inbox.jsonl`,
   filter to `emitted_at >= now - 7d`, project the LATEST line per `id`
   (acks supersede). Output:
   - Counts table: rows = repo, columns = severity (`info`/`warn`/`fail`).
   - **Top 3 unacked** — by severity then recency. Show id (first 8
     chars), severity, repo/source, summary (60 chars).
   - **Top 3 acked-with-resolution** — same shape, plus `resolution`
     text (truncated to 60 chars). Surfaces what got fixed this week.
3. **Quality snapshot deltas (last 7d)** — for each domain
   (`chatbot-qa`, `voicing-analysis`, `embeddings`, `invariants`, `e2e`),
   find the latest snapshot and the snapshot closest to 7 days ago.
   Compute the delta on the primary metric (per-domain mapping below)
   and emit a verdict:
   - **chatbot-qa**: metric = `pass_pct`; verdict thresholds — improved
     if Δ ≥ +0.02, regressed if Δ ≤ −0.02, stable otherwise; degraded
     if `pass_pct: null` in latest (env-degraded marker).
   - **voicing-analysis**: metric = `Corpus.Total`; same thresholds
     scaled to ±1% of baseline.
   - **embeddings**: metric = `leak_detection.full_classifier_accuracy`;
     CARE — *higher* leak accuracy is *worse* (more identity leak), so
     verdict inverts: improved if Δ ≤ −0.02, regressed if Δ ≥ +0.02.
   - **invariants**: metric = `exemplars.length` (proxy for coverage
     surface); improved if grew, regressed if shrank.
   - **e2e** (under `dashboard-playwright/`): metric = `summary.passed
     / summary.total`; same thresholds as chatbot-qa.
   - If the 7d-ago snapshot is missing, output `(no baseline)` and skip
     the delta — but still emit the latest value, so the reader sees
     the absolute level.
4. **`/grade-last-pr` verdicts (last 7d)** — walk
   `state/quality/pr-grades/*.json`, filter by `graded_at >= now − 7d`,
   bucket by `alignment` (`high` / `medium` / `low`). Output the counts
   table and **list every "low" PR** by `pr_number` + `title` (these
   are the misses worth re-reading; high/medium aren't named).
5. **`/test-plan` activity (last 7d)** — walk
   `state/quality/test-plans/*.meta.json`, filter by `generated_at >=
   now − 7d`. Output:
   - Total proposals (auto-fired count — anything with `generator`
     containing `test-plan-suggester.yml`).
   - **Developer-written follow-up rate** — informational; today this
     is always "n/a" because we don't yet record developer overrides.
     The placeholder reminds us to wire it in when that signal exists.
   - **PRs that had no test plan** — gh PRs merged in the window whose
     head SHAs don't have a corresponding `<sha>.md`. Names listed.
6. **`/council` activations (last 7d)** — walk
   `state/quality/council/*.json`, filter by `convened_at >= now − 7d`.
   Output count by `final_verdict` (`approve` / `request_changes` /
   `block`). **List every "block" verdict** by PR + one-way-door paths
   touched (these are the explicit one-way-door saves).

### Persistence

Each weekly run also writes the rendered digest to
`state/quality/weekly-digest/<YYYY-WW>.md` (ISO week — e.g.
`2026-W21.md`). The GitHub issue comment is *ephemeral* via the
sticky-replace pattern; the on-disk file is the durable audit trail.

The on-disk write happens in the workflow (so commits land in CI),
not in the interactive skill — running `/backlog-groom --digest` in
a session prints to the transcript and the user can manually save.

### Output template (appended to the existing top-3)

```markdown
---

## Last 7 days across all loops (<YYYY-MM-DD> → <YYYY-MM-DD>)

### Shipped PRs

| # | Title | SHA | Shipper | Category |
|---|---|---|---|---|
| 320 | feat(skill): /backlog-groom | 5e1e4c0 | claude-opus-4-7 | harness |
| ... |

### Algedonic signals

| Repo | info | warn | fail |
|---|---|---|---|
| ga | 2 | 1 | 0 |
| sentrux | 0 | 3 | 0 |

**Top 3 unacked:** ...

**Top 3 acked-with-resolution:** ...

### Quality snapshot deltas

| Domain | Latest | Δ vs 7d ago | Verdict |
|---|---|---|---|
| chatbot-qa | 0.94 | +0.02 | improved |
| voicing-analysis | 313047 | 0 | stable |
| embeddings | 0.747 | (no baseline) | n/a |
| invariants | 41 | 0 | stable |
| e2e | 5/5 | +1 | improved |

### `/grade-last-pr` verdicts

| Alignment | Count |
|---|---|
| high | 4 |
| medium | 1 |
| low | 0 |

_No "low" alignment PRs this week._

### `/test-plan` activity

- Auto-fired proposals: 3
- Developer-written follow-ups: n/a (not yet tracked)
- PRs without a test plan: 1 (#324)

### `/council` activations

| Verdict | Count |
|---|---|
| approve | 0 |
| request_changes | 0 |
| block | 0 |

_No council was convened this week._
```

## Anti-patterns

- **Re-reading the full backlog as text into the model.** The H3-bullet
  parse is the unit; full-text grep dilutes signal. If the parser
  misses something important, fix the parser; don't bypass it.
- **Scoring everything as Impact=H.** Then nothing is. The
  default-when-unsure is Impact=L. Force the L items to argue their
  way up.
- **Picking the easiest item.** Effort is the *last* tie-breaker, not
  the first. The seductive-easy item is how completion bias wins
  (see `feedback_completion_bias.md`).
- **Auto-promoting an in-flight item.** If `/digest`'s
  `success_criteria` says in-progress, the right move is usually
  "finish that," but surface it as a candidate with an explicit
  "carry-forward" tag — don't silently inherit.
- **Hiding the rubric.** The whole point is *defensible* recommendation.
  If a human can't see why item X beat item Y, the proposal is just
  another opaque LLM pick.
- **Auto-dispatching from the cron.** The weekly job posts a *comment*,
  not a PR. Wrong-priority weeks compound; the gate stays human.

## Related

- `/digest` (`.claude/skills/digest/SKILL.md`) — state cursor; this
  skill *reads* `state/digests/latest.md`, never writes it.
- `/grade-last-pr` (`.claude/skills/grade-last-pr/SKILL.md`) —
  post-merge intent-vs-delivery; complementary loop on the *output*
  side.
- `/learnings` — surprises, not work items. If grooming surfaces a
  pattern worth permanent recall, file a `/learnings` afterwards.
- `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md` —
  the parent plan; this skill is Phase 2 harness item #3.
- `ReactComponents/ga-react-components/src/dev-data/parsers.ts` —
  canonical BACKLOG.md parser. Mirror, don't reinvent.
- `state/backlog-grooming/README.md` — output template + directory
  retention policy.
