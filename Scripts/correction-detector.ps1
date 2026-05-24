# UserPromptSubmit hook — Enhancement 2 (Cherny auto /correct trigger).
# If the user's prompt matches correction language, nudge Claude to invoke
# /correct so the rule lands in CLAUDE.md. Throttled: fires once per N=5 prompts.

$ErrorActionPreference = 'SilentlyContinue'

$repoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { exit 0 }

$digestDir   = Join-Path $repoRoot 'state\digests'
$counterPath = Join-Path $digestDir '.correction-counter'

if (-not (Test-Path $digestDir)) {
    New-Item -ItemType Directory -Path $digestDir -Force | Out-Null
}

# Read JSON payload from stdin (UserPromptSubmit format: {prompt, session_id, ...})
$prompt = ''
try {
    $stdinRaw = [Console]::In.ReadToEnd()
    if ($stdinRaw) {
        $payload = $stdinRaw | ConvertFrom-Json
        if ($payload.prompt)      { $prompt = $payload.prompt }
        elseif ($payload.user_prompt) { $prompt = $payload.user_prompt }
    }
} catch {}

if (-not $prompt) { exit 0 }

# Match correction language (case-insensitive, anchored at start, word boundary)
$first = $prompt.Substring(0, [Math]::Min(200, $prompt.Length)).ToLowerInvariant()
if ($first -notmatch "^(no|don't|dont|stop|wait|actually|that's wrong|thats wrong|incorrect)\b") {
    exit 0
}

# Throttle: increment counter, fire only on first or every Nth thereafter
$throttle = 5
if ($env:GA_CORRECTION_THROTTLE -and $env:GA_CORRECTION_THROTTLE -match '^\d+$') {
    $throttle = [int]$env:GA_CORRECTION_THROTTLE
}

$count = 0
if (Test-Path $counterPath) {
    $raw = (Get-Content $counterPath -Raw).Trim()
    if ($raw -match '^\d+$') { $count = [int]$raw }
}
$count++
Set-Content -Path $counterPath -Value $count -Encoding UTF8

if ($count -ne 1 -and ($count % $throttle) -ne 0) { exit 0 }

$msg = 'Detected correction language. Consider invoking /correct to formalize this into CLAUDE.md so the rule persists across sessions (Cherny self-improvement loop).'

# UserPromptSubmit JSON output uses additionalContext to inject a system reminder.
$obj = [PSCustomObject]@{ additionalContext = "[correction-nudge] $msg" }
$obj | ConvertTo-Json -Compress
exit 0
