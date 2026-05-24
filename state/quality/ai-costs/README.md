# state/quality/ai-costs/

Per-PR estimated token spend for AI-agent-authored commits.

## What this directory holds

- `pricing.json` - provider rate card. Update when pricing changes.
- `SCHEMA.json` - JSON Schema for the per-PR spend files.
- `<head-sha>.json` - one file per analyzed PR (written by the workflow). Conforms to `pr-token-spend-v1` in `SCHEMA.json`.
- `README.md` (this file)

## Producer

`.github/workflows/pr-token-spend.yml` runs on every `pull_request` (opened, synchronize, reopened). It calls `Scripts/pr-token-tally.ps1` against the PR's commit range, posts a comment on the PR, and writes the per-PR JSON here.

## Consumer

The AI Contributors card at `/test#dev/summary` (source: `ReactComponents/ga-react-components/src/pages/DevelopmentSection.tsx`) can read these files via the dev-server middleware in `vite.config.ts`. The "Tokens left" / per-PR-cost column today reads "-" because no producer existed; this workflow is the producer. (UI wiring is a follow-up; the data contract is what's pinned here.)

## What this is and is not

**Is**: a cheap, reproducible upper-bound estimate of what an agent loop on a single PR probably cost. Useful for catching a $100 side quest before it lands.

**Is not**: a replacement for provider billing dashboards. Diff size is a weak proxy for actual API spend - a 1-line diff might be the tail end of a 200-tool-use exploration; a 1000-line diff might be a single mechanical search-and-replace. Always cross-check against Anthropic Console / OpenAI Usage / etc. for ground truth.

All numeric values use the `est_` prefix in JSON to keep this distinction explicit.

## Retention

Per-PR files are committed by the workflow and live in git history. There is no automated pruning yet. If the directory grows beyond a few hundred files we should:

1. Compress merged-PR files older than 90 days into a `archive/YYYY-MM.jsonl.gz`.
2. Keep only open-PR files in the live directory.

This is not implemented yet; expected scale is ~20-50 PR files per month, which is fine for at least 6-12 months.

## Pricing source caveats

`pricing.json` is a snapshot. The workflow logs a warning when `_as_of` is more than 90 days old. Each provider row carries its own `source_url` + `source_note` so any future maintainer can verify the number against the original pricing page. None of these rates are pulled live - that would require per-provider auth tokens (Anthropic Admin API, OpenAI Usage API, etc.), which is the documented next step but out of scope here.

## Threshold alerts

`threshold_alert_usd` in the per-PR JSON is `null` by default - no alerts fire. Future work: surface a configurable per-repo or per-agent budget cap that opens a `governance/agent-budget-exceeded` issue when crossed. Schema already allows the field; the workflow honors it if set via env var.

## Cross-repo notes

Sibling repos (ix, tars, Demerzel) follow the same `state/quality/` convention. If ix or Demerzel add their own PR spend trackers, please mirror this schema (or supersede it with a versioned `pr-token-spend-v2` and document the migration in `docs/contracts/`).
