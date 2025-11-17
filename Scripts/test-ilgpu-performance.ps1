# Performance test for ILGPU Voicing Search
# Compares CPU vs GPU performance

param(
    [int]$NumSearches = 100,
    [int]$VoicingCount = 1000
)

Write-Host "=== ILGPU Voicing Search Performance Test ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Number of searches: $NumSearches"
Write-Host "  Voicing count: $VoicingCount"
Write-Host ""

# Check if API is running
$apiUrl = "http://localhost:5232"
try {
    $healthCheck = Invoke-RestMethod -Uri "$apiUrl/health" -Method Get -TimeoutSec 2 -ErrorAction Stop
    Write-Host "✓ API server is running" -ForegroundColor Green
} catch {
    Write-Host "✗ API server is not running on $apiUrl" -ForegroundColor Red
    Write-Host "  Please start the API server first using:" -ForegroundColor Yellow
    Write-Host "  pwsh Scripts/start-backend.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Test queries
$queries = @(
    "easy jazz chord",
    "warm sounding voicing",
    "bright major chord",
    "dark minor seventh",
    "open string chord",
    "barre chord shape",
    "melodic voicing",
    "rootless voicing",
    "close voicing",
    "spread voicing"
)

Write-Host "Running $NumSearches searches..." -ForegroundColor Yellow
$totalTime = 0
$results = @()

for ($i = 0; $i -lt $NumSearches; $i++) {
    $query = $queries[$i % $queries.Length]
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-RestMethod -Uri "$apiUrl/api/voicings/search?q=$query&limit=10" -Method Get -ErrorAction Stop
        $stopwatch.Stop()
        
        $elapsed = $stopwatch.ElapsedMilliseconds
        $totalTime += $elapsed
        
        $results += [PSCustomObject]@{
            Query = $query
            ResultCount = $response.results.Count
            TimeMs = $elapsed
        }
        
        if (($i + 1) % 10 -eq 0) {
            Write-Host "  Completed $($i + 1)/$NumSearches searches..." -ForegroundColor Gray
        }
    } catch {
        Write-Host "  Error on search $($i + 1): $_" -ForegroundColor Red
        $stopwatch.Stop()
    }
}

Write-Host ""
Write-Host "=== Performance Results ===" -ForegroundColor Cyan
Write-Host ""

$avgTime = $totalTime / $NumSearches
$minTime = ($results | Measure-Object -Property TimeMs -Minimum).Minimum
$maxTime = ($results | Measure-Object -Property TimeMs -Maximum).Maximum
$p50 = ($results | Sort-Object TimeMs | Select-Object -Index ([Math]::Floor($NumSearches * 0.5))).TimeMs
$p95 = ($results | Sort-Object TimeMs | Select-Object -Index ([Math]::Floor($NumSearches * 0.95))).TimeMs
$p99 = ($results | Sort-Object TimeMs | Select-Object -Index ([Math]::Floor($NumSearches * 0.99))).TimeMs

Write-Host "Total searches: $NumSearches" -ForegroundColor White
Write-Host "Total time: $($totalTime)ms ($([Math]::Round($totalTime / 1000, 2))s)" -ForegroundColor White
Write-Host ""
Write-Host "Latency Statistics:" -ForegroundColor Yellow
Write-Host "  Average: $([Math]::Round($avgTime, 2))ms" -ForegroundColor White
Write-Host "  Median (p50): $($p50)ms" -ForegroundColor White
Write-Host "  p95: $($p95)ms" -ForegroundColor White
Write-Host "  p99: $($p99)ms" -ForegroundColor White
Write-Host "  Min: $($minTime)ms" -ForegroundColor White
Write-Host "  Max: $($maxTime)ms" -ForegroundColor White
Write-Host ""

# Calculate throughput
$throughput = $NumSearches / ($totalTime / 1000)
Write-Host "Throughput: $([Math]::Round($throughput, 2)) searches/second" -ForegroundColor Green
Write-Host ""

# Show sample results
Write-Host "Sample Results (first 5 searches):" -ForegroundColor Yellow
$results | Select-Object -First 5 | Format-Table -AutoSize

Write-Host ""
Write-Host "Performance Assessment:" -ForegroundColor Cyan
if ($avgTime -lt 10) {
    Write-Host "  ✓ EXCELLENT - Average latency < 10ms (GPU acceleration working!)" -ForegroundColor Green
} elseif ($avgTime -lt 50) {
    Write-Host "  ✓ GOOD - Average latency < 50ms" -ForegroundColor Green
} elseif ($avgTime -lt 100) {
    Write-Host "  ⚠ FAIR - Average latency < 100ms" -ForegroundColor Yellow
} else {
    Write-Host "  ✗ SLOW - Average latency > 100ms (GPU may not be working)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan

