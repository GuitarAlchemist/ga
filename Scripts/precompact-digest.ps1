# PreCompact hook — archives state/digests/latest.md and writes a metadata-only
# fallback if /digest wasn't invoked before compaction. The MODEL is the canonical
# writer (via /digest skill); this hook is a safety net. PreCompact stdin only
# carries session_id / cwd / hook_event_name — it cannot see the messages being
# compacted, so it cannot synthesize "Next action" / "In-flight" content.

$ErrorActionPreference = 'SilentlyContinue'
$WarningPreference     = 'SilentlyContinue'

$repoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { exit 0 }

$digestDir = Join-Path $repoRoot 'state\digests'
$archDir   = Join-Path $digestDir 'archive'
$latest    = Join-Path $digestDir 'latest.md'
New-Item -ItemType Directory -Path $digestDir, $archDir -Force | Out-Null

# Read PreCompact JSON payload from stdin
$sessionId = 'unknown'
try {
    $stdinRaw = [Console]::In.ReadToEnd()
    if ($stdinRaw) {
        $payload = $stdinRaw | ConvertFrom-Json
        if ($payload.session_id) { $sessionId = $payload.session_id }
    }
} catch {}

$ts     = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$tsFile = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH-mm-ssZ')

# Archive existing digest first (preserves model-driven content for audit)
if (Test-Path $latest) {
    Copy-Item $latest (Join-Path $archDir "$tsFile-$sessionId.md") -Force

    # If the existing digest is recent (<30 min), the model just ran /digest —
    # don't overwrite it with the hook's metadata-only stub. The archive copy
    # captures the moment-of-compaction state for audit either way.
    $age = (Get-Date) - (Get-Item $latest).LastWriteTime
    if ($age.TotalMinutes -lt 30) { exit 0 }
}

# Capture git state for the fallback stub
$branch   = & git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null
$headSha  = & git -C $repoRoot rev-parse --short HEAD 2>$null
$headSubj = & git -C $repoRoot log -1 --format='%s' 2>$null

$openPr = $null
if (Get-Command gh -ErrorAction SilentlyContinue) {
    $prJson = & gh pr view --json number 2>$null
    if ($prJson) {
        try { $openPr = "#$(($prJson | ConvertFrom-Json).number)" } catch {}
    }
}

$prLine = if ($openPr) { "**Open PR:** $openPr`n" } else { '' }

$digest = @"
---
schema_version: 1
session_id: $sessionId
written_at: $ts
trigger: precompact-hook-fallback
branch: $branch
head_sha: $headSha
head_subject: $headSubj
open_pr: $openPr
---

# Session digest (fallback — /digest was not invoked before compaction)

**Branch:** $branch @ $headSha — $headSubj
$prLine
## Model-driven sections

_No `/digest` invocation was captured before this compaction. Re-orient from
`git log` and the open PR. Invoke ``/digest`` mid-session to populate the
**Next action**, **In-flight**, **Live hypotheses**, **Open questions**, and
**Do NOT carry forward** sections before the next compaction event._
"@

Set-Content -Path $latest -Value $digest -Encoding UTF8
exit 0
