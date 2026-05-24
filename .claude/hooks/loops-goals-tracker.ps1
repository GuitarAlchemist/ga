# UserPromptSubmit + Stop hook — tracks /loop and /goal invocations across
# sessions so an operator opening a second Claude window can see what is
# currently looping or what goal is open.
#
# Mode is dispatched via the first CLI arg:
#   loops-goals-tracker.ps1 prompt   → UserPromptSubmit (read stdin, match /loop or /goal)
#   loops-goals-tracker.ps1 stop     → Stop hook (bump turn_count + last_activity_at)
#
# State files (both written, both gitignored):
#   1. Canonical (per-user, per-project, durable):
#      $env:USERPROFILE\.claude\projects\<encoded-repo-path>\state\runtime-loops-goals.jsonl
#   2. Repo-local mirror (readable by Vite dev server):
#      <repo>\state\.runtime-loops-goals.jsonl
#
# Append-only JSONL — one record per line. Schema:
#   {
#     "id": "<uuid>",
#     "kind": "loop" | "goal",
#     "started_at": "<iso8601>",
#     "session_id": "<string-from-hook-payload>",
#     "prompt_or_condition": "<truncated 280 chars>",
#     "turn_count": <int>,
#     "last_activity_at": "<iso8601>",
#     "status": "active" | "paused" | "completed" | "archived",
#     "event": "start" | "turn" | "status_change",
#     "branch": "<git branch at start>"
#   }
#
# Each event appends a new line — the projection that reads the file picks
# the latest record per id and uses that as the current state. This is
# the same append-only pattern used by state/algedonic/inbox.jsonl.

param(
    [Parameter(Mandatory=$false)][string]$Mode = 'prompt'
)

$ErrorActionPreference = 'SilentlyContinue'

# ─── Helpers ────────────────────────────────────────────────────────────

function Get-EncodedRepoPath {
    param([string]$RepoPath)
    # Claude Code encodes project paths under ~/.claude/projects/ by replacing
    # path separators and colons with dashes. Matches the existing convention
    # used by the memory MEMORY.md path (C--Users-spare-source-repos-ix).
    $encoded = $RepoPath -replace '[\\/:]', '-'
    return $encoded.TrimStart('-')
}

function Get-StatePaths {
    param([string]$RepoRoot)
    $encoded = Get-EncodedRepoPath -RepoPath $RepoRoot
    $userBase = Join-Path $env:USERPROFILE ".claude\projects\$encoded\state"
    $userPath = Join-Path $userBase 'runtime-loops-goals.jsonl'
    $repoPath = Join-Path $RepoRoot 'state\.runtime-loops-goals.jsonl'
    return @{
        UserPath = $userPath
        UserDir  = $userBase
        RepoPath = $repoPath
        RepoDir  = (Split-Path $repoPath -Parent)
    }
}

function Write-JsonlRecord {
    param(
        [hashtable]$Record,
        [string]$RepoRoot
    )
    $paths = Get-StatePaths -RepoRoot $RepoRoot
    New-Item -ItemType Directory -Path $paths.UserDir, $paths.RepoDir -Force -ErrorAction SilentlyContinue | Out-Null
    $line = ($Record | ConvertTo-Json -Compress -Depth 5)
    # Append to both. Use Out-File -Append for atomic-ish writes on Windows.
    Add-Content -Path $paths.UserPath -Value $line -Encoding UTF8 -ErrorAction SilentlyContinue
    Add-Content -Path $paths.RepoPath -Value $line -Encoding UTF8 -ErrorAction SilentlyContinue
}

function Get-ActiveRecords {
    # Latest-line-per-id projection over the repo-local mirror (cheap to read).
    param([string]$RepoRoot)
    $paths = Get-StatePaths -RepoRoot $RepoRoot
    if (-not (Test-Path $paths.RepoPath)) { return @() }
    $byId = @{}
    foreach ($line in (Get-Content $paths.RepoPath -ErrorAction SilentlyContinue)) {
        if (-not $line) { continue }
        try {
            $rec = $line | ConvertFrom-Json -ErrorAction Stop
            if ($rec.id) {
                $existing = $byId[$rec.id]
                if (-not $existing -or $rec.last_activity_at -ge $existing.last_activity_at) {
                    $byId[$rec.id] = $rec
                }
            }
        } catch { }
    }
    return @($byId.Values | Where-Object { $_.status -eq 'active' })
}

function Get-SafeText {
    param([string]$Value, [int]$MaxLen = 280)
    if (-not $Value) { return '' }
    $cleaned = $Value -replace '[\r\n\t]', ' '
    if ($cleaned.Length -gt $MaxLen) { $cleaned = $cleaned.Substring(0, $MaxLen) + '...' }
    return $cleaned
}

# ─── Main ───────────────────────────────────────────────────────────────

$repoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { exit 0 }
$repoRoot = $repoRoot -replace '/', '\'

$nowIso = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')

# Parse the hook payload from stdin. Claude Code sends JSON with .prompt for
# UserPromptSubmit and .session_id for both. Stop hook payload also includes
# .session_id.
$payload = $null
try {
    $raw = [Console]::In.ReadToEnd()
    if ($raw) { $payload = $raw | ConvertFrom-Json -ErrorAction Stop }
} catch { }

$sessionId = if ($payload -and $payload.session_id) { [string]$payload.session_id } else { 'unknown' }

if ($Mode -eq 'prompt') {
    # UserPromptSubmit: look for /loop or /goal at start of prompt.
    $prompt = if ($payload -and $payload.prompt) { [string]$payload.prompt } else { '' }
    if (-not $prompt) { exit 0 }

    $branch = & git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null

    # Match /loop [optional-interval] <command-or-prompt>
    # Match /goal <condition>
    $loopMatch = [regex]::Match($prompt, '^\s*/loop\b\s*(.*)$', 'Singleline')
    $goalMatch = [regex]::Match($prompt, '^\s*/goal\b\s*(.*)$', 'Singleline')

    if ($loopMatch.Success) {
        $captured = $loopMatch.Groups[1].Value.Trim()
        $rec = @{
            id                  = [guid]::NewGuid().ToString('N').Substring(0, 16)
            kind                = 'loop'
            started_at          = $nowIso
            session_id          = $sessionId
            prompt_or_condition = (Get-SafeText -Value $captured -MaxLen 280)
            turn_count          = 0
            last_activity_at    = $nowIso
            status              = 'active'
            event               = 'start'
            branch              = ([string]$branch).Trim()
        }
        Write-JsonlRecord -Record $rec -RepoRoot $repoRoot
    } elseif ($goalMatch.Success) {
        $captured = $goalMatch.Groups[1].Value.Trim()
        $rec = @{
            id                  = [guid]::NewGuid().ToString('N').Substring(0, 16)
            kind                = 'goal'
            started_at          = $nowIso
            session_id          = $sessionId
            prompt_or_condition = (Get-SafeText -Value $captured -MaxLen 280)
            turn_count          = 0
            last_activity_at    = $nowIso
            status              = 'active'
            event               = 'start'
            branch              = ([string]$branch).Trim()
        }
        Write-JsonlRecord -Record $rec -RepoRoot $repoRoot
    }
    exit 0
}

if ($Mode -eq 'stop') {
    # Stop hook: bump turn_count + last_activity_at for active records
    # belonging to this session.
    # Re-read raw lines so we can recover started_at as its original ISO string
    # (ConvertFrom-Json normalizes ISO-8601 strings to DateTime objects, which
    # then re-serialize as locale-formatted strings — breaks the schema).
    $paths = Get-StatePaths -RepoRoot $repoRoot
    $rawById = @{}
    if (Test-Path $paths.RepoPath) {
        foreach ($line in (Get-Content $paths.RepoPath -ErrorAction SilentlyContinue)) {
            if (-not $line) { continue }
            $idMatch = [regex]::Match($line, '"id"\s*:\s*"([^"]+)"')
            $startedMatch = [regex]::Match($line, '"started_at"\s*:\s*"([^"]+)"')
            if ($idMatch.Success -and $startedMatch.Success) {
                $rawById[$idMatch.Groups[1].Value] = $startedMatch.Groups[1].Value
            }
        }
    }

    $active = Get-ActiveRecords -RepoRoot $repoRoot
    foreach ($rec in $active) {
        if ($rec.session_id -ne $sessionId) { continue }
        $newTurn = [int]$rec.turn_count + 1
        $startedAtStr = if ($rawById.ContainsKey([string]$rec.id)) { $rawById[[string]$rec.id] } else { [string]$rec.started_at }
        $update = @{
            id                  = [string]$rec.id
            kind                = [string]$rec.kind
            started_at          = $startedAtStr
            session_id          = [string]$rec.session_id
            prompt_or_condition = [string]$rec.prompt_or_condition
            turn_count          = $newTurn
            last_activity_at    = $nowIso
            status              = 'active'
            event               = 'turn'
            branch              = [string]$rec.branch
        }
        Write-JsonlRecord -Record $update -RepoRoot $repoRoot
    }
    exit 0
}

exit 0
