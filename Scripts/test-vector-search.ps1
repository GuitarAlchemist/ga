# Test script for Vector Search API endpoints

$baseUrl = "http://localhost:5232/api/vectorsearch"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Vector Search API - Test Suite" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Test 1: Semantic Search - Dark jazz chords
Write-Host "Test 1: Semantic Search - 'dark moody jazz chords'" -ForegroundColor Yellow
Write-Host "GET $baseUrl/semantic?q=dark%20moody%20jazz%20chords&limit=3" -ForegroundColor Gray
try
{
    $results = Invoke-RestMethod -Uri "$baseUrl/semantic?q=dark%20moody%20jazz%20chords&limit=3"
    $results | ForEach-Object {
        Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
        Write-Host "    Quality: $( $_.quality ), Extension: $( $_.extension ), Score: $([math]::Round($_.score, 3) )" -ForegroundColor DarkGray
    }
}
catch
{
    Write-Host "  Error: $( $_.Exception.Message )" -ForegroundColor Red
    if ($_.Exception.Response.StatusCode -eq 503)
    {
        Write-Host "  Note: Make sure embeddings are generated and model files are in place" -ForegroundColor Yellow
    }
}
Write-Host ""

# Test 2: Semantic Search - Bright happy chords
Write-Host "Test 2: Semantic Search - 'bright happy major chords'" -ForegroundColor Yellow
Write-Host "GET $baseUrl/semantic?q=bright%20happy%20major%20chords&limit=3" -ForegroundColor Gray
try
{
    $results = Invoke-RestMethod -Uri "$baseUrl/semantic?q=bright%20happy%20major%20chords&limit=3"
    $results | ForEach-Object {
        Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
        Write-Host "    Quality: $( $_.quality ), Extension: $( $_.extension ), Score: $([math]::Round($_.score, 3) )" -ForegroundColor DarkGray
    }
}
catch
{
    Write-Host "  Error: $( $_.Exception.Message )" -ForegroundColor Red
}
Write-Host ""

# Test 3: Find Similar Chords - C minor triad (ID 1)
Write-Host "Test 3: Find Similar Chords to ID 1 (C minor triad)" -ForegroundColor Yellow
Write-Host "GET $baseUrl/similar/1?limit=5" -ForegroundColor Gray
try
{
    $results = Invoke-RestMethod -Uri "$baseUrl/similar/1?limit=5"
    $results | ForEach-Object {
        Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
        Write-Host "    Quality: $( $_.quality ), Extension: $( $_.extension ), Score: $([math]::Round($_.score, 3) )" -ForegroundColor DarkGray
    }
}
catch
{
    Write-Host "  Error: $( $_.Exception.Message )" -ForegroundColor Red
    if ($_.Exception.Message -like "*does not have an embedding*")
    {
        Write-Host "  Note: Run LocalEmbedding tool to generate embeddings" -ForegroundColor Yellow
    }
}
Write-Host ""

# Test 4: Hybrid Search - Dark jazz + Minor + Seventh
Write-Host "Test 4: Hybrid Search - 'dark jazz' + Minor + Seventh" -ForegroundColor Yellow
Write-Host "GET $baseUrl/hybrid?q=dark%20jazz&quality=Minor&extension=Seventh&limit=3" -ForegroundColor Gray
try
{
    $results = Invoke-RestMethod -Uri "$baseUrl/hybrid?q=dark%20jazz&quality=Minor&extension=Seventh&limit=3"
    $results | ForEach-Object {
        Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
        Write-Host "    Quality: $( $_.quality ), Extension: $( $_.extension ), Score: $([math]::Round($_.score, 3) )" -ForegroundColor DarkGray
    }
}
catch
{
    Write-Host "  Error: $( $_.Exception.Message )" -ForegroundColor Red
}
Write-Host ""

# Test 5: Hybrid Search - Complex modern + Quartal
Write-Host "Test 5: Hybrid Search - 'complex modern' + Quartal" -ForegroundColor Yellow
Write-Host "GET $baseUrl/hybrid?q=complex%20modern&stackingType=Quartal&limit=3" -ForegroundColor Gray
try
{
    $results = Invoke-RestMethod -Uri "$baseUrl/hybrid?q=complex%20modern&stackingType=Quartal&limit=3"
    $results | ForEach-Object {
        Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
        Write-Host "    Stacking: $( $_.stackingType ), Notes: $( $_.noteCount ), Score: $([math]::Round($_.score, 3) )" -ForegroundColor DarkGray
    }
}
catch
{
    Write-Host "  Error: $( $_.Exception.Message )" -ForegroundColor Red
}
Write-Host ""

# Test 6: Semantic Search - Simple beginner chords
Write-Host "Test 6: Semantic Search - 'simple beginner chords'" -ForegroundColor Yellow
Write-Host "GET $baseUrl/semantic?q=simple%20beginner%20chords&limit=3" -ForegroundColor Gray
try
{
    $results = Invoke-RestMethod -Uri "$baseUrl/semantic?q=simple%20beginner%20chords&limit=3"
    $results | ForEach-Object {
        Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
        Write-Host "    Quality: $( $_.quality ), Extension: $( $_.extension ), Notes: $( $_.noteCount )" -ForegroundColor DarkGray
    }
}
catch
{
    Write-Host "  Error: $( $_.Exception.Message )" -ForegroundColor Red
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Suite Complete" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "API Endpoints:" -ForegroundColor Yellow
Write-Host "  Semantic Search: $baseUrl/semantic?q={query}&limit={n}" -ForegroundColor White
Write-Host "  Similar Chords:  $baseUrl/similar/{id}?limit={n}" -ForegroundColor White
Write-Host "  Hybrid Search:   $baseUrl/hybrid?q={query}&quality={q}&extension={e}&limit={n}" -ForegroundColor White
Write-Host ""

Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Generate embeddings: dotnet run --project Apps/LocalEmbedding/LocalEmbedding.csproj" -ForegroundColor White
Write-Host "  2. Create vector index: mongosh < Scripts/create-vector-index.js" -ForegroundColor White
Write-Host "  3. Copy model files to API directory" -ForegroundColor White
Write-Host "  4. Restart API and run tests again" -ForegroundColor White
Write-Host ""

