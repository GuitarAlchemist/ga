#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Install Git hooks for Guitar Alchemist

.DESCRIPTION
    Configures Git to use custom hooks from .githooks directory

.EXAMPLE
    .\install-git-hooks.ps1
    Install Git hooks
#>

Write-Host "`n🎸 Guitar Alchemist - Install Git Hooks" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Get repository root
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

# Check if .git directory exists
if (-not (Test-Path ".git"))
{
    Write-Host "✗ Not a Git repository!" -ForegroundColor Red
    Write-Host "  This script must be run from the repository root.`n" -ForegroundColor Yellow
    exit 1
}

# Configure Git to use .githooks directory
Write-Host "▶ Configuring Git hooks directory..." -ForegroundColor Blue

git config core.hooksPath .githooks

if ($LASTEXITCODE -eq 0)
{
    Write-Host "✓ Git hooks directory configured" -ForegroundColor Green
}
else
{
    Write-Host "✗ Failed to configure Git hooks directory" -ForegroundColor Red
    exit 1
}

# Make hooks executable (on Unix-like systems)
if ($IsLinux -or $IsMacOS)
{
    Write-Host "`n▶ Making hooks executable..." -ForegroundColor Blue
    chmod +x .githooks/*
    Write-Host "✓ Hooks are now executable" -ForegroundColor Green
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "✓ Git hooks installed successfully!`n" -ForegroundColor Green

Write-Host "Available hooks:" -ForegroundColor Yellow
Write-Host "  • pre-commit - Checks formatting and builds before commit`n" -ForegroundColor White

Write-Host "To disable hooks temporarily:" -ForegroundColor Yellow
Write-Host "  git commit --no-verify`n" -ForegroundColor Cyan

Write-Host "To uninstall hooks:" -ForegroundColor Yellow
Write-Host "  git config --unset core.hooksPath`n" -ForegroundColor Cyan

