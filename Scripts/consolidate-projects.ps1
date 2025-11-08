# Consolidate duplicate projects into their Core counterparts
# Phase 2: GA.Business.UI, GA.Business.Graphiti, GA.Business.AI, GA.Business.Web

param(
    [string]$SourceProject = "GA.Business.UI",
    [string]$TargetProject = "GA.Business.Core.UI",
    [string]$OldNamespace = "GA.Business.UI",
    [string]$NewNamespace = "GA.Business.Core.UI"
)

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$commonPath = Join-Path $repoRoot "Common"
$sourcePath = Join-Path $commonPath $SourceProject
$targetPath = Join-Path $commonPath $TargetProject

Write-Host "🔄 Consolidating $SourceProject → $TargetProject" -ForegroundColor Cyan

# Step 1: Copy content from source to target
Write-Host "📋 Step 1: Copying content..." -ForegroundColor Yellow
if (-not (Test-Path $targetPath)) {
    New-Item -ItemType Directory -Path $targetPath | Out-Null
}

# Copy all files except bin/obj
Get-ChildItem -Path $sourcePath -Exclude "bin", "obj" -Recurse | ForEach-Object {
    $relativePath = $_.FullName.Substring($sourcePath.Length + 1)
    $targetFile = Join-Path $targetPath $relativePath
    
    if ($_.PSIsContainer) {
        if (-not (Test-Path $targetFile)) {
            New-Item -ItemType Directory -Path $targetFile | Out-Null
        }
    } else {
        Copy-Item -Path $_.FullName -Destination $targetFile -Force
    }
}
Write-Host "✅ Content copied" -ForegroundColor Green

# Step 2: Update namespaces in target project
Write-Host "📝 Step 2: Updating namespaces..." -ForegroundColor Yellow
Get-ChildItem -Path $targetPath -Include "*.cs", "*.razor" -Recurse | ForEach-Object {
    $content = Get-Content -Path $_.FullName -Raw
    $updated = $content -replace "namespace $OldNamespace", "namespace $NewNamespace"
    $updated = $updated -replace "using $OldNamespace", "using $NewNamespace"
    Set-Content -Path $_.FullName -Value $updated -NoNewline
}
Write-Host "✅ Namespaces updated" -ForegroundColor Green

# Step 3: Update .csproj file name
Write-Host "🔧 Step 3: Updating project file..." -ForegroundColor Yellow
$oldCsproj = Join-Path $targetPath "$SourceProject.csproj"
$newCsproj = Join-Path $targetPath "$TargetProject.csproj"

if (Test-Path $oldCsproj) {
    Rename-Item -Path $oldCsproj -NewName "$TargetProject.csproj"
}
Write-Host "✅ Project file renamed" -ForegroundColor Green

# Step 4: Update project references in solution
Write-Host "🔗 Step 4: Updating solution file..." -ForegroundColor Yellow
$slnFile = Join-Path $repoRoot "AllProjects.sln"
$slnContent = Get-Content -Path $slnFile -Raw

# Replace project reference
$slnContent = $slnContent -replace "Common\\$SourceProject\\$SourceProject\.csproj", "Common\$TargetProject\$TargetProject.csproj"
$slnContent = $slnContent -replace "`"$SourceProject`"", "`"$TargetProject`""

Set-Content -Path $slnFile -Value $slnContent -NoNewline
Write-Host "✅ Solution file updated" -ForegroundColor Green

# Step 5: Find and update all project references
Write-Host "🔍 Step 5: Updating project references..." -ForegroundColor Yellow
$csprojFiles = Get-ChildItem -Path $repoRoot -Include "*.csproj" -Recurse | Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\" }

$updateCount = 0
foreach ($csproj in $csprojFiles) {
    $content = Get-Content -Path $csproj.FullName -Raw
    if ($content -match $SourceProject) {
        $updated = $content -replace "Common\\$SourceProject\\$SourceProject\.csproj", "Common\$TargetProject\$TargetProject.csproj"
        if ($updated -ne $content) {
            Set-Content -Path $csproj.FullName -Value $updated -NoNewline
            $updateCount++
            Write-Host "  ✓ Updated: $($csproj.Name)" -ForegroundColor Gray
        }
    }
}
Write-Host "✅ Updated $updateCount project files" -ForegroundColor Green

# Step 6: Remove source project
Write-Host "🗑️  Step 6: Removing source project..." -ForegroundColor Yellow
git rm -r $sourcePath
Write-Host "✅ Source project removed from git" -ForegroundColor Green

Write-Host "✨ Consolidation complete: $SourceProject → $TargetProject" -ForegroundColor Green

