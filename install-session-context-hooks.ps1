# Git Hooks Installation for Guitar Alchemist
# Installs all pre-commit hooks with smart orchestration

Write-Host "🎸 Installing Git Pre-Commit Hooks..." -ForegroundColor Cyan
Write-Host ""

$hooksDir = ".git\hooks"

# Check if hooks directory exists
if (!(Test-Path $hooksDir)) {
    Write-Host "❌ Git hooks directory not found: $hooksDir" -ForegroundColor Red
    Write-Host "   Are you in the repository root?" -ForegroundColor Yellow
    exit 1
}

# Available hooks
$hooks = @{
    "pre-commit-general.ps1"         = "General quality checks (merge conflicts, secrets, syntax)"
    "pre-commit-session-context.ps1" = "Session context specific checks (builds, tests)"
    "pre-commit.ps1"                 = "Master orchestrator (runs appropriate checks)"
}

Write-Host "Available hooks:" -ForegroundColor Yellow
foreach ($hook in $hooks.Keys) {
    $description = $hooks[$hook]
    Write-Host "  ✓ $hook" -ForegroundColor Green
    Write-Host "    $description" -ForegroundColor Gray
}
Write-Host ""

# Backup existing pre-commit if it exists
$preCommitPath = "$hooksDir\pre-commit"
if (Test-Path $preCommitPath) {
    $backup = "$preCommitPath.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Write-Host "⚠️  Existing pre-commit hook found" -ForegroundColor Yellow
    Write-Host "   Backing up to: $backup" -ForegroundColor Gray
    Copy-Item $preCommitPath $backup
    Write-Host ""
}

# Verify all hook files exist
$allExist = $true
foreach ($hook in $hooks.Keys) {
    if (!(Test-Path "$hooksDir\$hook")) {
        Write-Host "❌ Hook file not found: $hook" -ForegroundColor Red
        $allExist = $false
    }
}

if (!$allExist) {
    Write-Host ""
    Write-Host "❌ Some hook files are missing. Cannot install." -ForegroundColor Red
    exit 1
}

# Install the master orchestrator
$masterHook = @"
#!/usr/bin/env pwsh
# Auto-generated master pre-commit hook
# Installed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

& "`$PSScriptRoot/pre-commit.ps1"
exit `$LASTEXITCODE
"@

Set-Content -Path $preCommitPath -Value $masterHook -Encoding UTF8

Write-Host "✅ Pre-commit hooks installed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 What happens on commit:" -ForegroundColor Cyan
Write-Host "  1. General checks (always run, ~5s)" -ForegroundColor Gray
Write-Host "     - Merge conflicts" -ForegroundColor Gray
Write-Host "     - Large files" -ForegroundColor Gray
Write-Host "     - Potential secrets" -ForegroundColor Gray
Write-Host "     - Basic syntax" -ForegroundColor Gray
Write-Host "     - Code quality" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Session context checks (only if those files changed, ~10s)" -ForegroundColor Gray
Write-Host "     - Domain/Business layer builds" -ForegroundColor Gray
Write-Host "     - Chatbot integration builds" -ForegroundColor Gray
Write-Host "     - Unit tests" -ForegroundColor Gray
Write-Host ""
Write-Host "🧪 Test the hooks now:" -ForegroundColor Yellow
Write-Host "   .git\hooks\pre-commit.ps1" -ForegroundColor White
Write-Host ""
Write-Host "💡 To bypass hooks (use sparingly):" -ForegroundColor Yellow
Write-Host "   git commit --no-verify -m `"message`"" -ForegroundColor White
Write-Host ""
Write-Host "📚 For more info:" -ForegroundColor Cyan
Write-Host "   .git\hooks\README.md" -ForegroundColor White
Write-Host ""
