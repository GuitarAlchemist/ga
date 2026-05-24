# ai-annotation-merge-gate.ps1 — PreToolUse(Bash) hook.
#
# Intercepts `gh pr merge ...` commands and blocks the merge if the
# reconciliation report (state/quality/ai-annotations-reconciliation.json)
# shows unresolved C (contradictory) or F (refuted, non-dismissed)
# annotations in files touched by the PR.
#
# Mirrors the Codex review gate shipped in PR #338 — same pattern, different
# signal. Exit code 2 with a message on stderr asks Claude (or the user) to
# either fix the underlying annotation or explicitly override.

$ErrorActionPreference = 'Continue'

try {
    $payload = $input | ConvertFrom-Json -ErrorAction Stop
    $command = $payload.tool_input.command
    if (-not $command) { exit 0 }

    # Only fire on `gh pr merge` (covers `--squash`, `--auto`, `-s`, etc.)
    if ($command -notmatch '\bgh\s+pr\s+merge\b') { exit 0 }

    # Allow an explicit override via env var (set once, expires with the shell).
    if ($env:AI_ANNOTATIONS_GATE_OVERRIDE -eq '1') {
        Write-Host "[ai-annotation gate] override active (AI_ANNOTATIONS_GATE_OVERRIDE=1) — allowing merge" -ForegroundColor Yellow
        exit 0
    }

    $repo = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
    $recon = Join-Path $repo 'state/quality/ai-annotations-reconciliation.json'
    if (-not (Test-Path $recon)) {
        # No reconciler output -> nothing to gate on. Don't block.
        exit 0
    }

    $data = $null
    try {
        $data = Get-Content -LiteralPath $recon -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    } catch {
        # Malformed report -> don't block (the next reconciler run will fix it).
        exit 0
    }

    if (-not $data.annotations) { exit 0 }

    # Discover PR-touched files. The current branch's diff against origin/main
    # is a fast approximation that doesn't require gh.
    $changedFiles = @()
    try {
        $diffOutput = & git diff --name-only origin/main...HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and $diffOutput) {
            $changedFiles = $diffOutput -split "`n" | Where-Object { $_.Trim().Length -gt 0 }
        }
    } catch {
        # Fall back to all annotations if git diff fails.
    }
    $changedSet = @{}
    foreach ($f in $changedFiles) {
        $normalized = $f.Trim().Replace('\', '/')
        if ($normalized) { $changedSet[$normalized] = $true }
    }

    $bad = New-Object System.Collections.ArrayList
    foreach ($a in $data.annotations) {
        $path = $a.location.path
        if ($changedSet.Count -gt 0 -and -not $changedSet.ContainsKey($path)) { continue }
        $tv = $a.truth_value
        $cert = $a.certainty
        if ($tv -eq 'C') {
            [void]$bad.Add(@{ kind = 'C'; path = $path; line = $a.location.line_start; claim = $a.claim })
        } elseif ($tv -eq 'F' -and $cert -ne 'dismissed') {
            [void]$bad.Add(@{ kind = 'F'; path = $path; line = $a.location.line_start; claim = $a.claim })
        }
    }

    if ($bad.Count -eq 0) { exit 0 }

    Write-Host ''
    Write-Host "[ai-annotation gate] Blocking 'gh pr merge' -- $($bad.Count) unresolved annotation(s) in changed files:" -ForegroundColor Red
    foreach ($b in $bad | Select-Object -First 10) {
        $line = "  [$($b.kind)] $($b.path):$($b.line) -- $($b.claim)"
        if ($line.Length -gt 220) { $line = $line.Substring(0, 220) + '...' }
        Write-Host $line -ForegroundColor Red
    }
    if ($bad.Count -gt 10) {
        Write-Host "  ... and $($bad.Count - 10) more" -ForegroundColor Red
    }
    Write-Host ''
    Write-Host "Resolve by:" -ForegroundColor Yellow
    Write-Host "  - fixing the underlying code so the next reconciler run flips the truth value" -ForegroundColor Yellow
    Write-Host "  - editing the @ai: marker (e.g., promote F -> C with [C:dismissed] if intentionally retired)" -ForegroundColor Yellow
    Write-Host '  - one-shot override: $env:AI_ANNOTATIONS_GATE_OVERRIDE=1; gh pr merge ...  (use only with reviewer sign-off)' -ForegroundColor Yellow
    Write-Host ''
    # Exit 2 -> Claude Code surfaces stderr to the assistant; the Bash call fails.
    exit 2
} catch {
    # Hooks must fail-open on internal errors.
    exit 0
}
