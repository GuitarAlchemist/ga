# state/backlog-grooming — Weekly Grooming Proposals

Per-grooming proposals produced by `/backlog-groom`
(`.claude/skills/backlog-groom/SKILL.md`). Each file is one ranked
top-3 work-item recommendation, written so the proposal survives
session compaction.

Phase 2 harness item #3 from
[`docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md`](../../docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md).

## Layout

```
state/backlog-grooming/
├── README.md                # this file (tracked, includes output template)
├── .gitkeep                 # preserve the directory pre-first-run
├── 2026-05-26.md            # one file per grooming run
├── 2026-06-02.md
└── ...
```

Filenames are `YYYY-MM-DD.md` (the date of the grooming run, not the
proposed work-week — easier to chronologically scan).

## Who writes

| Writer | Trigger | Content |
|---|---|---|
| `/backlog-groom` skill (interactive) | Model-invoked at session start or after a milestone | Full top-3 + rubric overrides + kill/keep decisions |
| `.github/workflows/weekly-backlog-grooming.yml` (cron) | Every Monday at 8:57 local | Same content, posted as comment on `[meta] Backlog grooming tracker` GitHub issue |

The skill output goes to **both** the user transcript and this
directory; the cron output goes to **both** this directory (via PR or
direct push) and the tracker issue.

## Who reads

- **Humans** — the load-bearing reader. Top-3 is approved or vetoed
  before any agent dispatches work.
- **The next `/backlog-groom` run** — pulls the prior week's proposal
  to detect "we proposed X last week, did X happen?" drift.
- **The next session's agent** — once the human approves the top-3,
  the agent reads the proposal as authoritative direction (use the
  `First slice` field as the ticket).

## Retention

- **Tracked in git** so historical priorities are auditable. Trends
  matter (did we keep proposing the same thing for 4 weeks because
  no one picked it up?).
- **No automatic deletion.** The directory will grow ~52 files/year;
  acceptable. If it crosses 200 files, add an `archive/<year>/`
  subdir manually.

## Distinctions from adjacent artifacts

| Artifact | Captures | When |
|---|---|---|
| `state/backlog-grooming/<date>.md` | **proposal** — ranked top-3 work for the human to approve | weekly + on demand |
| `state/digests/latest.md` | **state** — current cursor, in-flight, hypotheses | per session, gitignored |
| `state/quality/pr-grades/<sha>.json` | **post-merge grade** — intent vs delivery | per merge |
| `BACKLOG.md` | **canonical list of ideas** — read by the parser | edited by humans / `/feature` |
| `docs/plans/<date>-<title>.md` | **plan for a specific item** — written after grooming approves it | per item |

The chain is:

```
BACKLOG.md  →  /backlog-groom  →  state/backlog-grooming/<date>.md
              (read + score)         (proposed top-3)
                       ↓
               human approves
                       ↓
             docs/plans/<date>-<title>.md   ← plan for the picked item
                       ↓
                 PR ships
                       ↓
       state/quality/pr-grades/<sha>.json   ← grade-last-pr loop
```

## Output template

`/backlog-groom` writes this exact structure (also embedded in
`.claude/skills/backlog-groom/SKILL.md`):

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
top-3 gets vetoed by the human. Capped at 3 to avoid bloat.

---

## Kill / keep decisions surfaced

- **Dormant plan:** `docs/plans/<file>.md` (last touched <date>) —
  recommend <KILL / REVIVE> because <one sentence>.

---

## Rubric overrides applied

If you deviated from the default scoring (e.g. demoted a high-impact
item because it's a one-way door without a council), document why
here so the next grooming can audit the call.
```

## Cron workflow

[`.github/workflows/weekly-backlog-grooming.yml`](../../.github/workflows/weekly-backlog-grooming.yml)
runs every Monday at 8:57 local (13:57 UTC). The off-:00 mark dodges
cron stampedes from the rest of the ecosystem (quality-snapshot,
gemini-scheduled-triage, etc. all sit on :00).

The workflow:

1. Locates or creates a long-lived issue titled
   `[meta] Backlog grooming tracker`.
2. Posts the current top-3 proposal as a new comment, tagging
   @spareilleux for the go/no-go.
3. Does **not** dispatch work, open PRs, or modify branches. Wrong-
   priority weeks compound; the gate stays human.

The headless invocation of the skill is the lightweight variant —
without a Claude runtime in CI, the workflow assembles the proposal
from the same disk artifacts the skill reads (BACKLOG.md, open
issues, quality snapshots, latest digest, stale plans) using `gh`
and `jq`. The interactive skill is richer (it can ask clarifying
questions and apply taste); the cron variant is the floor that
ensures a proposal exists every Monday even if no human runs the
skill.
