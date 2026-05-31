---
title: README drift sensor — keeping cross-repo READMEs in sync over time
status: living
date: 2026-05-17
related:
  - Scripts/readme-drift-survey.ps1
  - .github/workflows/readme-drift-sensor.yml
  - state/quality/readme-drift/baseline.json
trigger: "user observation 2026-05-17 — 'README.md on all repos don't look up to date'"
---

# README drift sensor

A weekly cross-repo check that surfaces stale READMEs as a durable signal
instead of a once-and-forget sweep.

## Problem

READMEs decay silently. They get written once, then features ship without
touching them, and six weeks later the README claims `.NET 9` while
`global.json` pins `.NET 10`. Contributors lose trust in the doc as a source
of truth, and onboarding cost rises.

The four-repo GuitarAlchemist ecosystem (`ga` + `ix` + `Demerzel` + `tars`)
plus the broader sibling fleet makes this worse: drift in any one repo
breaks the cross-references in the others. The 2026-05-17 audit found
four READMEs >30 days behind their HEAD commits, all of them load-bearing.

## Mechanism

```
Monday 08:00 UTC
   │
   ▼
.github/workflows/readme-drift-sensor.yml
   │
   ├─→ Checkout ga + every sibling in baseline.tracked_repos
   │
   ├─→ pwsh Scripts/readme-drift-survey.ps1
   │      • for each repo: compute drift = (HEAD date − last README touch)
   │      • status = ok | borderline (14d) | stale (30d) | very-stale (60d)
   │      • emit state/quality/readme-drift/YYYY-MM-DD.json
   │
   ├─→ Commit snapshot to ga main (filename matches ix-quality-trend loader)
   │
   └─→ Open or update single tracking issue listing stale repos
          (closes itself when every repo returns to OK)
```

The signal is **last commit that touched README.md**, not filesystem mtime.
Mtime is fragile (changes on local edits even before commit, doesn't change
on rebase, etc.). The committed-touch date is the authoritative answer to
"when was this README intentionally updated."

## Thresholds (lower-is-better drift)

Set in `state/quality/readme-drift/baseline.json` under `thresholds_days`:

| Status | Drift | What it means |
|---|---|---|
| **ok** | < 14 days | README touched recently; assume current |
| **borderline** | 14–29 days | Drift is starting; worth a glance |
| **stale** | 30–59 days | Likely missing recent features; PR refresh |
| **very-stale** | ≥ 60 days | Definitely missing things; refresh blocker |

Stale and very-stale repos trigger the tracking issue. Borderline does not
(too much noise).

## What the sensor does NOT check (v1 limits)

- **Content drift**: it doesn't read the README and check claims (e.g., "is the
  port number listed still accurate?"). That's a v2 feature requiring an
  LLM-based comparator skill — see baseline.json `_open_gaps.semantic_drift`.
- **Per-repo enforcement**: the sensor lives in `ga` only. v2 could propagate
  the workflow into each sibling so each repo emits its own drift check on
  its own PRs.
- **Auto-remediation**: the sensor surfaces the gap; refresh PRs are still
  drafted by a human (or operator-supervised agent via `/octo:develop`).
  v2 could add a `/readme-sync` skill that drafts auto-PRs.

These are tracked under baseline.json `_open_gaps`.

## Adding a new sibling repo

Two places to update together (they're cross-asserted at survey-time):

1. **`state/quality/readme-drift/baseline.json`** — add to `tracked_repos[]`.
2. **`.github/workflows/readme-drift-sensor.yml`** — add to the `for repo in …`
   loop in the "Checkout siblings" step.

The survey script also has a hard-coded list at the top, kept in sync.

## Remediation flow

When the tracking issue surfaces a stale README:

1. **Survey the repo's HEAD** — what's actually shipped since the last
   README touch? `git log --since=<last_touch> --oneline`.
2. **Draft a refresh PR** — match the [ga README refresh template][ga-pr] for
   voice + section structure. Correct any factual errors (port numbers,
   versions, layer model, license).
3. **Commit message format**: `docs: refresh <repo> README — <N> days stale`.
4. **PR body**: list what's new, what's corrected, what's still missing.
5. **Merge** — the sensor's next run will close the tracking issue
   automatically when drift returns to OK.

[ga-pr]: https://github.com/GuitarAlchemist/ga/pull/254

## How this compares to other quality domains

The README-drift domain follows the same shape as `chatbot-qa`,
`embeddings`, `voicing-analysis`, `optick-sae`:

- `state/quality/<domain>/baseline.json` — schema-pinned contract
- daily-or-weekly CI workflow producing `YYYY-MM-DD.json`
- `ix-quality-trend` consumes the snapshots cross-domain
- baseline has `_harness` for `/auto-optimize` integration (currently
  `roundtrip_validator: null` — operator-supervised remediation only)

Future: when `/readme-sync` skill exists, the loop pattern from
`chatbot-qa-roundtrip-validate` + the embeddings validator could apply to
README drift too — propose a refresh, validate it didn't break links or
references, commit if green.

## Related

- `state/quality/readme-drift/baseline.json` — the contract.
- `Scripts/readme-drift-survey.ps1` — the survey script.
- `.github/workflows/readme-drift-sensor.yml` — the weekly cron.
- `docs/methodology/multi-llm-review.md` — sibling discipline doc.
- `.claude/skills/auto-optimize/SKILL.md` — the generalized loop driver
  (README drift could plug in once `/readme-sync` exists).
