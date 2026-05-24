# dashboard-playwright

Per-run JSON snapshots from the `.github/workflows/playwright-dashboard.yml`
job (harness item #7 — outside-in browser verification of the dashboard
and the chatbot showcase).

## File layout

```
state/quality/e2e/dashboard-playwright/
├── README.md              (this file)
├── .gitkeep
└── <ISO-8601 UTC>.json    (one per workflow run on main)
```

## Why a sub-directory?

The top-level `state/quality/e2e/` is also written to by the post-merge-smoke
workflow (curl results). Each E2E producer gets its own subdirectory so
two parallel post-push workflows can commit without colliding.

## Snapshot schema (1.0.0)

```jsonc
{
  "schema_version": "1.0.0",
  "kind": "e2e-dashboard-playwright",
  "timestamp": "2026-05-23T19-04-12Z",
  "git_sha": "abc1234…",
  "git_ref": "refs/heads/main",
  "run_id": "9876543210",
  "outcome": "success",        // or "failure" / "cancelled"
  "base_url": "https://demos.guitaralchemist.com",
  "summary": { "total": 5, "passed": 5, "failed": 0, "skipped": 0 },
  "tests": [
    {
      "title": "Dashboard loads › /test returns 200 and renders core chrome",
      "file": "tests/dashboard/dashboard-loads.spec.ts",
      "status": "passed",
      "duration_ms": 1842,
      "error": null
    }
  ]
}
```

## Test coverage

| Spec | What it catches |
|---|---|
| `dashboard-loads.spec.ts` | `/test` 5xx, blank-page render bug, console errors, tab labels missing |
| `heartbeat-banner.spec.ts` | `/test#dev/summary` heartbeat banner color, epic-shipped % rendered |
| `manifest-page.spec.ts` | `/test/manifest` route 404, Copy curl removed, schema_version absent |
| `chatbot-showcase.spec.ts` | `/chatbot/` showcase modal stuck loading, response > 5s SLO |
| `harness-tab.spec.ts` | `/test#dev/harness` rollout table truncated, baseline tiles fail |

## Trend

Read latest:

```bash
ls -1t state/quality/e2e/dashboard-playwright/*.json | head -1 | xargs jq '.summary'
```

Failures over last N runs:

```bash
for f in $(ls -1t state/quality/e2e/dashboard-playwright/*.json | head -20); do
  jq -r '[.timestamp, .summary.failed, .summary.total] | @tsv' "$f"
done
```
