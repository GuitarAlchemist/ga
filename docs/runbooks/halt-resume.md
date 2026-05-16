---
title: Operator runbook — halt / resume autonomous loops across the ecosystem
status: living
date: 2026-05-16
related:
  - docs/contracts/2026-05-16-overseer-halt-marker.contract.md
  - docs/contracts/overseer-halt-marker.schema.json
  - docs/plans/2026-05-16-arch-demerzel-overseer-extension-plan.md
  - docs/plans/2026-05-16-feat-development-process-overseer-plan.md
---

# Halt / resume autonomous loops

When you need to pause every `/auto-optimize` loop in every repo (GA, ix, Demerzel, tars) immediately. Use this when:

- A loop is producing unexpected output and you need a moment to inspect
- Cloud-cost burn is climbing faster than expected
- You're cutting a release branch and want zero autonomous churn during the freeze
- An operator on another machine pushed something you need to investigate before agents react

## TL;DR

| You want | Command |
|---|---|
| Halt everything now | `mcp__demerzel__demerzel_governance halt-all "reason"` |
| Resume everything | `Remove-Item (Join-Path $env:USERPROFILE '.demerzel\HALT-ALL')` |
| Check halt state | `Test-Path (Join-Path $env:USERPROFILE '.demerzel\HALT-ALL')` |

## How it works

The mechanism is **one file**, by design:

```
$env:USERPROFILE\.demerzel\HALT-ALL    # Windows
~/.demerzel/HALT-ALL                   # macOS / Linux
```

Every `/auto-optimize` loop runner in every repo checks for this file at Step 0 of each cycle, BEFORE doing anything else. If the file exists and is valid, the loop prints the halt reason and exits 0.

The format is JSON validated against [`docs/contracts/overseer-halt-marker.schema.json`](../contracts/overseer-halt-marker.schema.json). Minimum:

```json
{
  "schema_version": 1,
  "halted_at": "2026-05-16T16:30:00Z",
  "reason": "Investigating cost burn"
}
```

## Halting from Claude Code (recommended)

Once the Demerzel ACP Phase 1 endpoints land, the canonical halt looks like this:

```
/mcp demerzel halt-all "Investigating runaway cost burn"
```

This:
1. Writes `~/.demerzel/HALT-ALL` with `halted_by: "demerzel-acp"` and your reason
2. Appends an audit event (once Phase 2 ships)
3. Returns a confirmation showing all halted scopes

To resume:

```
/mcp demerzel resume-all
```

## Halting by hand (operator fallback)

If Demerzel is down OR the MCP tool isn't wired, write the file directly:

```powershell
# Windows
$haltDir = Join-Path $env:USERPROFILE '.demerzel'
New-Item -ItemType Directory -Path $haltDir -Force | Out-Null
@{
  schema_version = 1
  halted_at      = (Get-Date).ToUniversalTime().ToString('o')
  halted_by      = "operator:$env:USERNAME"
  reason         = 'Investigating runaway cost burn'
  scope          = 'loops-only'
  expires_at     = $null
} | ConvertTo-Json | Set-Content (Join-Path $haltDir 'HALT-ALL') -Encoding UTF8
```

```bash
# macOS / Linux
mkdir -p ~/.demerzel
cat > ~/.demerzel/HALT-ALL <<EOF
{
  "schema_version": 1,
  "halted_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "halted_by": "operator:$USER",
  "reason": "Investigating runaway cost burn",
  "scope": "loops-only",
  "expires_at": null
}
EOF
```

To resume:

```powershell
Remove-Item (Join-Path $env:USERPROFILE '.demerzel\HALT-ALL')
```

```bash
rm ~/.demerzel/HALT-ALL
```

## Time-limited halts

Set `expires_at` to an RFC3339 UTC timestamp; consumers will treat the marker as absent after that time. Useful for scheduled freezes:

```json
{
  "schema_version": 1,
  "halted_at": "2026-05-16T16:30:00Z",
  "halted_by": "operator:spareilleux",
  "reason": "Mobile release branch cut — freeze through Sunday",
  "scope": "loops-only",
  "expires_at": "2026-05-19T00:00:00Z"
}
```

## Exempting specific agents

If you want to halt everyone EXCEPT one agent (e.g. you're actively debugging that one), add to `exempt_agents`:

```json
{
  "schema_version": 1,
  "halted_at": "2026-05-16T16:30:00Z",
  "reason": "Debugging chatbot-qa loop in isolation",
  "exempt_agents": ["auto-optimize/chatbot-qa-debug"]
}
```

## Per-repo killswitch is still here

The cross-repo HALT-ALL is **additive**. The per-repo killswitches still work and are the offline fallback:

```powershell
# Halt only this repo's loops (existing mechanism)
pwsh Scripts/loop-killswitch.ps1 -Set -Reason "Local debugging"

# Halt only one domain in this repo (existing mechanism)
Set-Content "state/quality/$domain/.STOP" "Per-domain pause"
```

If `~/.demerzel/` is unreachable for any reason (network share, permissions, missing directory), the consumer falls back to `state/.loop-halted` for the repo it's running in. No outage of the overseer freezes a repo whose local killswitch isn't set.

## Verifying the halt worked

After issuing a halt, run the dev-process-overseer in any repo:

```powershell
pwsh Scripts/dev-process-overseer.ps1 -Domain <any-domain>
```

Expected output includes `[BLOCK] cross-repo-halt-all` (once the overseer's Phase 1 HALT-ALL check lands — see `docs/plans/2026-05-16-feat-development-process-overseer-plan.md` Phase 2). Until then, attempt to start a cycle manually; the SKILL.md Step 0 check should refuse and print the halt reason.

## What HALT-ALL does NOT do

- Does NOT kill processes that are already running mid-cycle (it's a cycle-boundary check)
- Does NOT block manual `/digest` writes, plan-doc edits, or PR review
- Does NOT propagate to CI workflows (those are their own concern; CI killswitch is `gh workflow disable`)
- Does NOT replace the per-domain `.STOP` markers — those still control scope inside a repo

## Common questions

**Q: I halted but a loop just committed anyway.**
A: Check the loop's Step 0 implementation. If it predates the Phase 1 SKILL.md update (this PR), it doesn't check the marker yet. The strategic plan's Phase 4 will add hard CI-side enforcement; Phase 1 is opt-in by the loop scripts that follow the updated SKILL.md.

**Q: I removed the file but loops are still paused.**
A: Loops check at Step 0 of each cycle, not continuously. The next cycle invocation will see the absence and proceed. If you're seeing persistent halt messages, check for a stale per-repo `state/.loop-halted` too.

**Q: I want to halt ONE repo only.**
A: Use the per-repo mechanism (`Scripts/loop-killswitch.ps1 -Set`). HALT-ALL is for ecosystem-wide pauses.

**Q: Can two operators halt at the same time?**
A: The file is single-writer by design. Last writer wins; the previous reason gets archived to `~/.demerzel/halts/<timestamp>.json` per the contract. v1 doesn't lock against races — if you need exclusive halt control, that's a future feature.
