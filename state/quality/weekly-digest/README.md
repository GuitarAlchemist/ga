# state/quality/weekly-digest — Weekly trend digests

Persistent archive of the **"Last 7 days across all loops"** trend digest
produced every Monday by `.github/workflows/weekly-backlog-grooming.yml`.

The companion comment on the `[meta] Backlog grooming tracker` issue is
*ephemeral* (sticky-replaced each week by ISO-week marker); the on-disk
file here is the durable audit trail.

## Layout

```
state/quality/weekly-digest/
├── README.md         ← this file
├── 2026-W21.md       ← one file per ISO week
├── 2026-W22.md
└── ...
```

One file per **ISO week** (e.g. `2026-W21.md` covers Mon May 18 →
Sun May 24, 2026). ISO-week naming sorts chronologically and is
language-neutral.

## Schema

Each file is plain markdown matching the **"Weekly digest mode"** section
of `.claude/skills/backlog-groom/SKILL.md`. Sections in order:

1. **Shipped PRs** — table of PRs merged in the window.
2. **Algedonic signals** — counts table + top-3 unacked + top-3
   acked-with-resolution from `state/algedonic/inbox.jsonl`.
3. **Quality snapshot deltas** — per-domain latest vs 7d-ago value and
   verdict (improved / regressed / stable / degraded / n/a).
4. **`/grade-last-pr` verdicts** — counts by alignment, names of any
   "low" PRs.
5. **`/test-plan` activity** — auto-fired count + PRs without a plan.
6. **`/council` activations** — counts by final verdict, names of any
   "block" verdicts.

The first line of each file is an HTML comment marker
(`<!-- weekly-digest-YYYY-WW -->`) so the workflow can find and
sticky-replace its prior tracker-issue comment of the same week.

## Retention

**Indefinite.** Files are small (a few KB each, ~50/year) and useful
for end-of-quarter retrospectives, anniversary "what shipped last May"
queries, and post-mortem timeline reconstruction. No GC.

## Generator

- Workflow: `.github/workflows/weekly-backlog-grooming.yml`
  (step: "Assemble \"Last 7 days\" digest")
- Skill contract: `.claude/skills/backlog-groom/SKILL.md`
  (section: "Weekly digest mode")
- Companion real-time channel: `state/algedonic/` — this digest is the
  trend-level supervisor; algedonic is the seconds-to-hours incident
  layer.
