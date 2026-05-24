# ai-annotation-scan.ps1 — PostToolUse(Edit|Write) hook.
#
# After Claude (or any tool) edits a file, scan that single file for new or
# changed @ai: annotations and append them to
# state/quality/ai-annotations.jsonl. This is the incremental complement to
# the full ix-ai-annotations CLI which scans the whole workspace.
#
# Best-effort by design: hooks should never block the agent. Every failure
# path silently exits 0 — the next full scan from ix will reconcile.
#
# See docs/contracts/2026-05-24-ai-annotation.contract.md (in ix) for the
# marker syntax + JSONL schema.

$ErrorActionPreference = 'Continue'

try {
    $payload = $input | ConvertFrom-Json -ErrorAction Stop
    $filePath = $payload.tool_input.file_path
    if (-not $filePath) { exit 0 }
    if (-not (Test-Path $filePath)) { exit 0 }

    # Resolve repo root (the working directory of the agent).
    $repo = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
    $outDir = Join-Path $repo 'state/quality'
    $outFile = Join-Path $outDir 'ai-annotations.jsonl'
    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }

    # Extensions we'll scan. Mirrors the Rust walker's SCANNABLE_EXTS.
    $scanExts = @('.rs', '.cs', '.fs', '.fsx', '.fsi', '.ts', '.tsx', '.js', '.jsx',
                  '.py', '.rb', '.go', '.java', '.swift', '.c', '.h', '.cpp', '.hpp',
                  '.lua', '.sql', '.hs', '.sh', '.ps1', '.psm1', '.yml', '.yaml',
                  '.toml', '.html', '.htm', '.xml', '.md')
    $ext = [System.IO.Path]::GetExtension($filePath).ToLowerInvariant()
    if ($scanExts -notcontains $ext) { exit 0 }

    # Repo-relative path with forward slashes (matches the Rust walker output).
    $relPath = $filePath
    if ($filePath.StartsWith($repo, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relPath = $filePath.Substring($repo.Length).TrimStart('\', '/').Replace('\', '/')
    }

    # Pattern matches the same syntax as crates/ix-ai-annotations/src/parser.rs.
    # Comment markers: //, ///, //!, #, --, /*, <!--.
    $commentRe = '^\s*(?:///|//!|//|#!|#|--|/\*|<!--)\s*'
    $markerRe = '@ai:([a-zA-Z_-]+)\s+(.+?)\s+\[([TPUDFC]):([a-z][a-z\-]*)(?:\s+conf:([0-9]*\.?[0-9]+))?(?:\s+src:([^\]]+?))?\]'

    $kindsValid = @('invariant', 'assumption', 'hypothesis', 'contract', 'smell', 'decision', 'hint')
    $certValid = @('test', 'formal-proof', 'manually-reviewed', 'assumed', 'uncertain', 'inferred', 'dismissed')

    $now = (Get-Date).ToUniversalTime().ToString("o")
    $lines = Get-Content -LiteralPath $filePath -ErrorAction Stop
    $newEntries = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($line -notmatch $commentRe) { continue }
        if ($line -notmatch $markerRe) { continue }
        $kind = $Matches[1]
        $claim = $Matches[2].Trim()
        $tv = $Matches[3]
        $cert = $Matches[4]
        $confStr = $Matches[5]
        $evidence = if ($Matches[6]) { $Matches[6].Trim() } else { $null }
        if ($kindsValid -notcontains $kind) { continue }
        if ($certValid -notcontains $cert) { continue }
        $conf = if ($confStr) { [double]$confStr } else { 0.5 }
        $lineNum = $i + 1

        # Deterministic id mirroring types::annotation_id (sha256 of path:line:Kind:claim).
        # The Rust code uses `{:?}` Debug format for the kind, which produces e.g. "Invariant".
        $kindPascal = ($kind.Substring(0, 1).ToUpper() + $kind.Substring(1).ToLower()).Replace('-', '')
        $idKey = "{0}:{1}:{2}:{3}" -f $relPath, $lineNum, $kindPascal, $claim
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($idKey)
            $hash = $sha.ComputeHash($bytes)
            $hashHex = -join ($hash | ForEach-Object { $_.ToString('x2') })
        } finally {
            $sha.Dispose()
        }
        $id = "sha256:$hashHex"

        $entry = [ordered]@{
            schema_version = 1
            id             = $id
            kind           = $kind
            claim          = $claim
            truth_value    = $tv
            certainty      = $cert
            confidence     = $conf
            source         = [ordered]@{
                author = 'claude'
            }
            location       = [ordered]@{
                path       = $relPath
                line_start = $lineNum
                line_end   = $lineNum
            }
            created_at     = $now
            updated_at     = $now
        }
        if ($evidence) { $entry.source.evidence = $evidence }
        [void]$newEntries.Add($entry)
    }

    if ($newEntries.Count -eq 0) { exit 0 }

    # Read existing JSONL (if any), drop lines for this file (we replace
    # per-file entries entirely on each hook fire), then append the fresh set.
    $existing = @()
    if (Test-Path $outFile) {
        $existing = Get-Content -LiteralPath $outFile -ErrorAction SilentlyContinue | Where-Object {
            $_.Trim().Length -gt 0
        } | Where-Object {
            try {
                $obj = $_ | ConvertFrom-Json -ErrorAction Stop
                $obj.location.path -ne $relPath
            } catch {
                $false
            }
        }
    }

    $newLines = $newEntries | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 6 }
    Set-Content -LiteralPath $outFile -Value (@($existing) + @($newLines)) -Encoding UTF8
    exit 0
} catch {
    # Hooks must not block the agent.
    exit 0
}
