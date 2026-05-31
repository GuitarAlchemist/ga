# state/quality/e2e — Post-merge smoke artifacts

Per-merge "is the live demo still up?" snapshots produced by
[`.github/workflows/post-merge-smoke.yml`](../../../.github/workflows/post-merge-smoke.yml)
and (locally) by [`Scripts/post-merge-smoke.ps1`](../../../Scripts/post-merge-smoke.ps1).

Item #4 of [docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md](../../../docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md).

## Why this exists

The session of 2026-05-23 lost ~25 minutes to a port-displacement outage
(`https://demos.guitaralchemist.com/` was returning empty / 502 because a
secondary Vite instance had stolen port 5176). No automation detected it;
the user had to surface it manually. This directory captures the signal so
the next outage of that class is caught within minutes of the bad merge.

## Layout

```
state/quality/e2e/
├── README.md                          # this file
├── SCHEMA.json                        # JSON Schema for the artifact
├── .gitkeep                           # preserve the directory pre-first-run
└── 2026-05-23T23-58-12Z.json          # one artifact per merge to main
└── 2026-05-23T23-59-04Z.json
└── ...
```

Filenames are ISO-8601 UTC with `:` replaced by `-` for Windows safety:
`YYYY-MM-DDTHH-MM-SSZ.json`.

## Artifact schema (v1)

See [`SCHEMA.json`](SCHEMA.json) for the formal schema. Shape:

```json
{
  "schema": "post-merge-smoke-v1",
  "run_at": "2026-05-23T23:58:12Z",
  "head_sha": "abc123...",
  "all_passed": true,
  "tunnel_down": false,
  "unreachable_count": 0,
  "checks": [
    {
      "url": "https://demos.guitaralchemist.com/test",
      "status": 200,
      "ok": true,
      "latency_ms": 234,
      "size_bytes": 1542,
      "content_marker": "Guitar Alchemist",
      "content_marker_found": true,
      "unreachable": false
    }
  ]
}
```

### Field notes

- **`all_passed`** — true iff every check has `ok: true`. False triggers
  the comment + tracking issue path in the workflow.
- **`tunnel_down`** — true iff `>=2` of the 3 URLs come back unreachable
  (connection refused / timeout). When true the workflow logs a warning
  and exits 0 — no issue is opened, since the cause is almost certainly
  the user's local Cloudflare tunnel being down, not a code regression.
- **`content_marker`** — substring asserted in the response body. Catches
  the "HTML served, but Vite was serving the wrong project" failure mode.
- **`size_bytes`** — UTF-8 byte count of the response body. Guards against
  the empty-200 case (server up, content not generated).

## Retention policy

Keep all artifacts. Each is < 2 KB; ~1000 merges/year ≈ 2 MB/year. Don't
prune. If we ever need to GC, drop one of the older ones before changing
schema rather than dropping a recent one.

## When the schema changes

Bump the `schema` discriminator (`post-merge-smoke-v1` → `v2`) and update
`SCHEMA.json`. Old artifacts stay parseable — downstream consumers MUST
gate on the `schema` field, not assume the latest shape.

## How a consumer reads these

There's no consumer yet beyond humans and the workflow itself. Future
candidates:

- `ix-quality-trend` could load these alongside the existing categories
  (`embeddings/`, `voicing-analysis/`, `chatbot-qa/`, `readme-drift/`).
  Doing so would require a date-derived filename rather than the
  ISO-with-seconds we use today; if we add the loader, change naming to
  `YYYY-MM-DD-<sha7>.json` then. For now, raw JSON.
- The `/test#dev/harness` dashboard tab could surface "last smoke status"
  by reading the newest artifact. Item #4 ships only the producer.

## Local run

```powershell
pwsh Scripts/post-merge-smoke.ps1                  # check only, no artifact
pwsh Scripts/post-merge-smoke.ps1 -WriteArtifact   # check + write JSON here
```

Exit codes:

- `0` — all checks passed (or tunnel-down detected)
- `1` — at least one URL failed and it's not a tunnel-down case
- `2` — artifact write failed (rare; usually a disk/permission issue)
