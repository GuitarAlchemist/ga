# state/algedonic — VSM algedonic-channel inbox

Append-only inbox for cross-repo algedonic signals.

- **Contract:** `docs/contracts/2026-05-24-algedonic-channel.contract.md`
- **Schema:** `docs/contracts/algedonic-signal.schema.json`
- **Storage:** `inbox.jsonl` — one JSON object per line, never overwritten.

## What lives here

Each line is one signal emitted by some repo in the ecosystem (`ga`, `ix`,
`demerzel`, `tars`, `sentrux`, `hari`) or by the GA pull-mode poller workflow
(`.github/workflows/ecosystem-health.yml`). Signals carry severity, summary,
details (markdown), evidence URL, affected artifacts, and an ack record.

Acks are themselves new lines (same `id`, `ack.acked = true`); the projector
takes the latest line per `id` as canonical. Supersedes lets a new signal hide
a previous one without rewriting history.

## How to emit

PowerShell helper (GA-side):

```powershell
pwsh Scripts/algedonic-emit.ps1 `
    -Severity warn `
    -Repo ga `
    -Source quality-snapshot `
    -Summary "Voicing analysis pass_pct dropped to 0.91" `
    -Details "Baseline 0.94 -> 0.91 over last 3 runs." `
    -EvidenceUrl "https://github.com/GuitarAlchemist/ga/actions/runs/..." `
    -AffectedArtifacts state/quality/voicing-analysis/2026-05-24.json
```

Sibling-language helpers (Python for IX/sentrux/hari, F# for tars) land in
follow-up PRs per the contract's rollout plan §14.

## How to ack

```powershell
pwsh Scripts/algedonic-emit.ps1 -Ack -Id <signal-id> -AckBy <handle> -Resolution "fixed in #320"
```

Or click the **Ack** button on the Heartbeat algedonic tile in
`/test#dev/summary` (Vite dashboard, `/dev-data/algedonic/ack/<id>`).

## How to read

The Vite dev-data middleware projects the latest state from `inbox.jsonl` at
`/dev-data/algedonic`. The Heartbeat tile in `OverviewSection.tsx` calls that
endpoint every 30s.

## Retention

Acked signals older than `acked_at + ttl_hours` are garbage-collectable. The
collector is not in this PR; the inbox can grow to ~10 MB / ~50,000 records
before line reads slow down. A future maintenance script will compact into
`archive/<year>/<month>.jsonl.gz`.

## Security

Public-by-design, same as `state/quality/`. **Never** put secrets, PII, or
private user data in any field. The dashboard write endpoint
(`/dev-data/algedonic/ack/<id>`) is local-only via the existing `isLocalOrigin`
guard in `ReactComponents/ga-react-components/vite.config.ts`.
