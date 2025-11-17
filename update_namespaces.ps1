# PowerShell script to update namespaces in moved embedding service files

$files = Get-ChildItem -Path "Common/GA.Business.Core.AI/Services/Embeddings/*.cs" -Recurse

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    
    # Update namespaces
    $content = $content -replace "namespace GA\.Data\.MongoDB\.Services\.Embeddings;", "namespace GA.Business.Core.AI.Services.Embeddings;"
    $content = $content -replace "namespace GA\.Data\.SemanticKernel\.Embeddings;", "namespace GA.Business.Core.AI.Services.Embeddings;"
    
    # Update using statements
    $content = $content -replace "using GA\.Data\.MongoDB\.Services\.Embeddings;", "using GA.Business.Core.AI.Services.Embeddings;"
    $content = $content -replace "using GA\.Data\.SemanticKernel\.Embeddings;", "using GA.Business.Core.AI.Services.Embeddings;"
    
    Set-Content -Path $file.FullName -Value $content -NoNewline
    Write-Host "Updated: $($file.Name)"
}

Write-Host "Namespace update complete!"
