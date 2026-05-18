# Validates state/digests/latest.md frontmatter against docs/contracts/digest-schema.json.
# Karpathy R11: every AI step declares an output schema; runtime rejects mismatches.
# Exit 0 = valid; non-zero = invalid with reason on stderr.

param([string]$DigestPath)

$ErrorActionPreference = 'SilentlyContinue'

$repoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { exit 0 }

if (-not $DigestPath) {
    $DigestPath = Join-Path $repoRoot 'state\digests\latest.md'
}

if (-not (Test-Path $DigestPath)) {
    # Nothing to validate; not an error condition.
    exit 0
}

$content = Get-Content $DigestPath -Raw
if ($content -notmatch '(?s)^---\r?\n(.*?)\r?\n---') {
    Write-Error "digest-validate: missing or malformed YAML frontmatter in $DigestPath"
    exit 1
}

$fmRaw = $matches[1]
$fm = @{}
foreach ($line in ($fmRaw -split "`r?`n")) {
    if ($line -match '^\s*([\w_]+)\s*:\s*(.*?)\s*$') {
        $key = $matches[1]
        $val = $matches[2]
        if ($val -eq 'null' -or $val -eq '') { $val = $null }
        $fm[$key] = $val
    }
}

# Required fields per docs/contracts/digest-schema.json
$required = @('schema_version', 'session_id', 'written_at', 'trigger', 'branch', 'head_sha', 'head_subject')
foreach ($k in $required) {
    if (-not $fm.ContainsKey($k) -or $null -eq $fm[$k]) {
        Write-Error "digest-validate: required field '$k' missing or null"
        exit 1
    }
}

if ($fm['schema_version'] -ne '1') {
    Write-Error "digest-validate: schema_version must be 1 (got '$($fm['schema_version'])')"
    exit 1
}

$validTriggers = @('digest-skill', 'precompact-hook-fallback', 'stop-hook-finalize', 'auto-write-routine')
if ($fm['trigger'] -notin $validTriggers) {
    Write-Error "digest-validate: trigger '$($fm['trigger'])' not in $($validTriggers -join ', ')"
    exit 1
}

exit 0
