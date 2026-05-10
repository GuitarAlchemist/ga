# Inspects the changed files in the current branch (vs main) and prints
# whether the change requires the Demerzel QA Architect tribunal +
# multi-LLM review before merge.
#
# Used by /chatbot-iterate to surface gates upfront, and can be wired
# into a CI step or the Stop hook for an extra reminder.
#
# Exits 0 in all cases — output is informational, not a blocker.
# Print to stdout so the Claude Code session transcript captures it.

$ErrorActionPreference = 'SilentlyContinue'

# Get changed files vs main. Falls back to staged/unstaged if branch is
# main itself.
$branch = & git branch --show-current 2>$null
if (-not $branch) { exit 0 }

$changed = if ($branch -eq 'main') {
    & git diff --name-only HEAD 2>$null
} else {
    & git diff --name-only main...HEAD 2>$null
}

if (-not $changed) {
    Write-Host "No changes vs main — no chatbot tribunal gate needed." -ForegroundColor Gray
    exit 0
}

# Path-based classification of which gates a change triggers.
# Source-of-truth for "what counts as chatbot core" — keep this in sync
# with the iron-law block in .claude/skills/chatbot-iterate/SKILL.md.
$gatePatterns = @{
    Tribunal = @(
        'Common/GA\.Business\.ML/Agents',
        'Common/GA\.Business\.ML/.*Mcp',
        'Apps/ga-server/GaApi/Mcp',
        'Common/GA\.Business\.DSL/',
        'IChatClientFactory',
        'AddGuitarAlchemistA[iI]',
        'Common/GA\.Business\.ML/Skills',
        'Common/GA\.Business\.ML/Routing',
        'Common/GA\.Business\.ML/Intents'
    )
    OctopusReview = @(
        'Common/GA\.Business\.ML/',
        'Apps/ga-server/GaApi/',
        'Apps/GaChatbot\.Api/',
        'Common/GA\.Business\.Core/'
    )
}

function Test-Match($file, $patterns) {
    foreach ($p in $patterns) {
        if ($file -match $p) { return $true }
    }
    return $false
}

$tribunalHits = @()
$octopusHits  = @()
foreach ($f in $changed) {
    if (Test-Match $f $gatePatterns.Tribunal)      { $tribunalHits += $f }
    if (Test-Match $f $gatePatterns.OctopusReview) { $octopusHits  += $f }
}

Write-Host ''
Write-Host "Chatbot tribunal-gate check on $branch" -ForegroundColor Cyan
Write-Host ('  ' + $changed.Count + ' file(s) changed vs main') -ForegroundColor Gray

if ($tribunalHits.Count -gt 0) {
    Write-Host ''
    Write-Host '  TRIBUNAL REQUIRED — Demerzel QA Architect verdict' -ForegroundColor Red
    Write-Host '  Multi-LLM review REQUIRED — /octo:review or codex' -ForegroundColor Red
    Write-Host ''
    Write-Host '  Tribunal-triggering paths:' -ForegroundColor Yellow
    foreach ($h in ($tribunalHits | Select-Object -First 10)) {
        Write-Host "    $h" -ForegroundColor DarkYellow
    }
    if ($tribunalHits.Count -gt 10) {
        Write-Host ('    ... and ' + ($tribunalHits.Count - 10) + ' more') -ForegroundColor DarkGray
    }
    Write-Host ''
    Write-Host '  feedback_multi_llm_review_pays_off: PR #151 evidence — multi-LLM review' -ForegroundColor DarkGray
    Write-Host '  caught 9 real bugs that local tests missed. This gate is load-bearing.' -ForegroundColor DarkGray
} elseif ($octopusHits.Count -gt 0) {
    Write-Host ''
    Write-Host '  octo:review recommended (touches ML / API / chatbot adjacency)' -ForegroundColor Yellow
    Write-Host '  Tribunal NOT required (no core agent / MCP / DSL paths touched)' -ForegroundColor Green
} else {
    Write-Host ''
    Write-Host '  No chatbot gates triggered — standard review only' -ForegroundColor Green
}
Write-Host ''
exit 0
