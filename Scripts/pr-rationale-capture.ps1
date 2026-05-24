# PostToolUse(matcher=Bash) hook — Enhancement 3 (Cherny PR rationale capture).
# When a Bash invocation runs `gh pr create`, snapshot the title + body + diff
# stats to state/digests/pr-<num>-<slug>.md so the rationale survives later edits.

$ErrorActionPreference = 'SilentlyContinue'

$repoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { exit 0 }

# Read PostToolUse JSON payload from stdin: {tool_input: {command}, tool_response: {output}}
$cmd = ''
$output = ''
try {
    $stdinRaw = [Console]::In.ReadToEnd()
    if (-not $stdinRaw) { exit 0 }
    $payload = $stdinRaw | ConvertFrom-Json
    if ($payload.tool_input -and $payload.tool_input.command) { $cmd = $payload.tool_input.command }
    if ($payload.tool_response) {
        if ($payload.tool_response.output) { $output = $payload.tool_response.output }
        elseif ($payload.tool_response -is [string]) { $output = $payload.tool_response }
    }
} catch { exit 0 }

if (-not $cmd) { exit 0 }
if ($cmd -notmatch 'gh\s+pr\s+create') { exit 0 }

# Extract --title "..." or --title '...'
$title = ''
$mTitle = [regex]::Match($cmd, '--title\s+(?:"([^"]*)"|''([^'']*)'')')
if ($mTitle.Success) {
    $title = if ($mTitle.Groups[1].Value) { $mTitle.Groups[1].Value } else { $mTitle.Groups[2].Value }
}

# Extract --body "..." or --body '...' or --body "$(cat <<'EOF'...EOF)" (best-effort)
$body = ''
$mBody = [regex]::Match($cmd, '--body\s+(?:"([\s\S]*?)"(?=\s|$)|''([\s\S]*?)''(?=\s|$))')
if ($mBody.Success) {
    $body = if ($mBody.Groups[1].Value) { $mBody.Groups[1].Value } else { $mBody.Groups[2].Value }
}

# Extract PR number from gh CLI output URL (https://github.com/x/y/pull/123)
$prNum = 'unknown'
$mPr = [regex]::Match($output, 'pull/(\d+)')
if ($mPr.Success) { $prNum = $mPr.Groups[1].Value }

# Slug from title
$slug = ($title.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
if ($slug.Length -gt 40) { $slug = $slug.Substring(0, 40).Trim('-') }
if (-not $slug) { $slug = 'untitled' }

$digestDir = Join-Path $repoRoot 'state\digests'
if (-not (Test-Path $digestDir)) { New-Item -ItemType Directory -Path $digestDir -Force | Out-Null }
$outPath = Join-Path $digestDir "pr-$prNum-$slug.md"

$tsIso     = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$branch    = & git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null
$shortStat = & git -C $repoRoot diff --shortstat HEAD~1 2>$null
if (-not $shortStat) { $shortStat = '' }

$md = @"
---
schema_version: 1
trigger: pr-rationale-capture
captured_at: $tsIso
branch: $branch
pr_number: $prNum
diff_shortstat: $shortStat
---

# PR #$prNum — $title

**Branch:** $branch
**Captured:** $tsIso
**Diff:** $shortStat

## Title

$title

## Body

$body
"@

Set-Content -Path $outPath -Value $md -Encoding UTF8
exit 0
