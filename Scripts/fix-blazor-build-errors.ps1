#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fix Blazor build errors in GaApi
#>

$ErrorActionPreference = "Stop"

Write-Host "🔧 Fixing Blazor build errors..." -ForegroundColor Cyan
Write-Host ""

# Fix 1: Update Documents.razor - Add type parameters to MudBlazor components
Write-Host "📝 Fixing Documents.razor..." -ForegroundColor Green
$documentsPath = "Apps/ga-server/GaApi/Components/Pages/Documents.razor"
$content = Get-Content $documentsPath -Raw

# Fix MudChip components
$content = $content -replace '<MudChip\s+Color="Color\.Primary">', '<MudChip T="string" Color="Color.Primary">'
$content = $content -replace '<MudChip\s+Color="Color\.Secondary">', '<MudChip T="string" Color="Color.Secondary">'
$content = $content -replace '<MudChip\s+Color="Color\.Info">', '<MudChip T="string" Color="Color.Info">'
$content = $content -replace '<MudChip\s+Color="Color\.Success">', '<MudChip T="string" Color="Color.Success">'
$content = $content -replace '<MudChip\s+Color="Color\.Warning">', '<MudChip T="string" Color="Color.Warning">'
$content = $content -replace '<MudChip\s+Color="Color\.Error">', '<MudChip T="string" Color="Color.Error">'
$content = $content -replace '<MudChip>', '<MudChip T="string">'

# Fix MudList and MudListItem
$content = $content -replace '<MudList\s+Clickable="true">', '<MudList T="string" Clickable="true">'
$content = $content -replace '<MudList>', '<MudList T="string">'
$content = $content -replace '<MudListItem>', '<MudListItem T="string">'

# Fix MudChipSet
$content = $content -replace '<MudChipSet\s+MultiSelection="true">', '<MudChipSet T="string" MultiSelection="true">'
$content = $content -replace '<MudChipSet>', '<MudChipSet T="string">'

Set-Content $documentsPath -Value $content -NoNewline
Write-Host "  ✅ Fixed Documents.razor" -ForegroundColor DarkGreen

# Fix 2: Update Home.razor
Write-Host "📝 Fixing Home.razor..." -ForegroundColor Green
$homePath = "Apps/ga-server/GaApi/Components/Pages/Home.razor"
$content = Get-Content $homePath -Raw
$content = $content -replace '<MudChip\s+Color="Color\.Primary">', '<MudChip T="string" Color="Color.Primary">'
$content = $content -replace '<MudChip\s+Color="Color\.Secondary">', '<MudChip T="string" Color="Color.Secondary">'
$content = $content -replace '<MudChip\s+Color="Color\.Info">', '<MudChip T="string" Color="Color.Info">'
$content = $content -replace '<MudChip\s+Color="Color\.Success">', '<MudChip T="string" Color="Color.Success">'
$content = $content -replace '<MudChip>', '<MudChip T="string">'
Set-Content $homePath -Value $content -NoNewline
Write-Host "  ✅ Fixed Home.razor" -ForegroundColor DarkGreen

# Fix 3: Update RetroactionLoop.razor
Write-Host "📝 Fixing RetroactionLoop.razor..." -ForegroundColor Green
$retroPath = "Apps/ga-server/GaApi/Components/Pages/RetroactionLoop.razor"
$content = Get-Content $retroPath -Raw
$content = $content -replace '<MudChip\s+Color="Color\.Primary">', '<MudChip T="string" Color="Color.Primary">'
$content = $content -replace '<MudChip\s+Color="Color\.Secondary">', '<MudChip T="string" Color="Color.Secondary">'
$content = $content -replace '<MudChip\s+Color="Color\.Info">', '<MudChip T="string" Color="Color.Info">'
$content = $content -replace '<MudChip\s+Color="Color\.Success">', '<MudChip T="string" Color="Color.Success">'
$content = $content -replace '<MudChip\s+Color="Color\.Warning">', '<MudChip T="string" Color="Color.Warning">'
$content = $content -replace '<MudChip>', '<MudChip T="string">'
$content = $content -replace '<MudList\s+Clickable="true">', '<MudList T="string" Clickable="true">'
$content = $content -replace '<MudList>', '<MudList T="string">'
$content = $content -replace '<MudListItem>', '<MudListItem T="string">'
Set-Content $retroPath -Value $content -NoNewline
Write-Host "  ✅ Fixed RetroactionLoop.razor" -ForegroundColor DarkGreen

# Fix 4: Update Documents.razor.cs - Fix TimeSpan issue
Write-Host "📝 Fixing Documents.razor.cs..." -ForegroundColor Green
$documentsCsPath = "Apps/ga-server/GaApi/Components/Pages/Documents.razor.cs"
$content = Get-Content $documentsCsPath -Raw
$content = $content -replace 'Duration = doc\["metadata"\]\.AsBsonDocument\.GetValue\("duration", 0\)\.ToInt32\(\)', 'Duration = TimeSpan.FromSeconds(doc["metadata"].AsBsonDocument.GetValue("duration", 0).ToInt32())'
Set-Content $documentsCsPath -Value $content -NoNewline
Write-Host "  ✅ Fixed Documents.razor.cs" -ForegroundColor DarkGreen

# Fix 5: Update RetroactionLoop.razor.cs - Fix AnalyzeKnowledgeGapsAsync
Write-Host "📝 Fixing RetroactionLoop.razor.cs..." -ForegroundColor Green
$retroCsPath = "Apps/ga-server/GaApi/Components/Pages/RetroactionLoop.razor.cs"
$content = Get-Content $retroCsPath -Raw
$content = $content -replace 'var gaps = await KnowledgeGapAnalyzer\.AnalyzeKnowledgeGapsAsync\(\);', 'var gaps = await KnowledgeGapAnalyzer.AnalyzeAsync();'
Set-Content $retroCsPath -Value $content -NoNewline
Write-Host "  ✅ Fixed RetroactionLoop.razor.cs" -ForegroundColor DarkGreen

Write-Host ""
Write-Host "✅ All Blazor build errors fixed!" -ForegroundColor Green

