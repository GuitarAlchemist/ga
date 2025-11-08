# Test script for Enhanced Vector Search API endpoints

$baseUrl = "http://localhost:5232/api"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Enhanced Vector Search API - Test Suite" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Test 1: Get available strategies
Write-Host "Test 1: Get Available Strategies" -ForegroundColor Yellow
Write-Host "GET $baseUrl/vectorsearchstrategy/available" -ForegroundColor Gray
try {
    $strategies = Invoke-RestMethod -Uri "$baseUrl/vectorsearchstrategy/available"
    Write-Host "Available strategies:" -ForegroundColor Green
    $strategies.PSObject.Properties | ForEach-Object {
        $name = $_.Name
        $perf = $_.Value
        Write-Host "  [$name]" -ForegroundColor White
        Write-Host "    Expected Search Time: $($perf.expectedSearchTime)" -ForegroundColor DarkGray
        Write-Host "    Memory Usage: $($perf.memoryUsageMB) MB" -ForegroundColor DarkGray
        Write-Host "    Requires GPU: $($perf.requiresGPU)" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 2: Get current strategy
Write-Host "Test 2: Get Current Strategy" -ForegroundColor Yellow
Write-Host "GET $baseUrl/vectorsearchstrategy/current" -ForegroundColor Gray
try {
    $current = Invoke-RestMethod -Uri "$baseUrl/vectorsearchstrategy/current"
    Write-Host "Current strategy:" -ForegroundColor Green
    Write-Host "  Name: $($current.name)" -ForegroundColor White
    Write-Host "  Available: $($current.isAvailable)" -ForegroundColor White
    if ($current.performance) {
        Write-Host "  Expected Search Time: $($current.performance.expectedSearchTime)" -ForegroundColor DarkGray
        Write-Host "  Memory Usage: $($current.performance.memoryUsageMB) MB" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 3: Get strategy requirements
Write-Host "Test 3: Get Strategy Requirements" -ForegroundColor Yellow
Write-Host "GET $baseUrl/vectorsearchstrategy/requirements" -ForegroundColor Gray
try {
    $requirements = Invoke-RestMethod -Uri "$baseUrl/vectorsearchstrategy/requirements"
    Write-Host "Strategy requirements:" -ForegroundColor Green
    $requirements.PSObject.Properties | ForEach-Object {
        $name = $_.Name
        $req = $_.Value
        Write-Host "  [$name] - $($req.description)" -ForegroundColor White
        Write-Host "    Estimated Search Time: $($req.estimatedSearchTime)" -ForegroundColor DarkGray
        Write-Host "    Memory Usage: $($req.memoryUsage)" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 4: Try to switch to InMemory strategy
Write-Host "Test 4: Switch to InMemory Strategy" -ForegroundColor Yellow
Write-Host "POST $baseUrl/vectorsearchstrategy/switch/InMemory" -ForegroundColor Gray
try {
    $switchResult = Invoke-RestMethod -Uri "$baseUrl/vectorsearchstrategy/switch/InMemory" -Method Post
    Write-Host "Switch result:" -ForegroundColor Green
    Write-Host "  Message: $($switchResult.message)" -ForegroundColor White
    if ($switchResult.strategy) {
        Write-Host "  New Strategy: $($switchResult.strategy.name)" -ForegroundColor White
    }
} catch {
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response.StatusCode -eq 400) {
        Write-Host "  Note: Strategy may not be available or already active" -ForegroundColor Yellow
    }
}
Write-Host ""

# Test 5: Auto-select best strategy
Write-Host "Test 5: Auto-Select Best Strategy" -ForegroundColor Yellow
Write-Host "POST $baseUrl/vectorsearchstrategy/auto-select" -ForegroundColor Gray
try {
    $autoResult = Invoke-RestMethod -Uri "$baseUrl/vectorsearchstrategy/auto-select" -Method Post
    Write-Host "Auto-selection result:" -ForegroundColor Green
    Write-Host "  Message: $($autoResult.message)" -ForegroundColor White
    Write-Host "  Reason: $($autoResult.reason)" -ForegroundColor White
    if ($autoResult.strategy) {
        Write-Host "  Selected Strategy: $($autoResult.strategy.name)" -ForegroundColor White
    }
} catch {
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 6: Get performance stats
Write-Host "Test 6: Get Performance Statistics" -ForegroundColor Yellow
Write-Host "GET $baseUrl/vectorsearchstrategy/stats" -ForegroundColor Gray
try {
    $stats = Invoke-RestMethod -Uri "$baseUrl/vectorsearchstrategy/stats"
    Write-Host "Performance statistics:" -ForegroundColor Green
    $stats.PSObject.Properties | ForEach-Object {
        $name = $_.Name
        $stat = $_.Value
        Write-Host "  [$name]" -ForegroundColor White
        Write-Host "    Total Chords: $($stat.totalChords)" -ForegroundColor DarkGray
        Write-Host "    Memory Usage: $($stat.memoryUsageMB) MB" -ForegroundColor DarkGray
        Write-Host "    Total Searches: $($stat.totalSearches)" -ForegroundColor DarkGray
        if ($stat.averageSearchTime -and $stat.averageSearchTime -ne "00:00:00") {
            Write-Host "    Average Search Time: $($stat.averageSearchTime)" -ForegroundColor DarkGray
        }
    }
} catch {
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 7: Run benchmark
Write-Host "Test 7: Run Performance Benchmark" -ForegroundColor Yellow
Write-Host "POST $baseUrl/vectorsearchstrategy/benchmark?iterations=5" -ForegroundColor Gray
try {
    $benchmark = Invoke-RestMethod -Uri "$baseUrl/vectorsearchstrategy/benchmark?iterations=5" -Method Post
    Write-Host "Benchmark results:" -ForegroundColor Green
    Write-Host "  Current Strategy: $($benchmark.currentStrategy)" -ForegroundColor White
    Write-Host "  Iterations: $($benchmark.iterations)" -ForegroundColor White
    Write-Host "  Fastest: $($benchmark.fastest)" -ForegroundColor White
    Write-Host "  Recommendation: $($benchmark.recommendation)" -ForegroundColor White
    Write-Host "  Results:" -ForegroundColor White
    $benchmark.results.PSObject.Properties | ForEach-Object {
        $name = $_.Name
        $result = $_.Value
        Write-Host "    [$name]: $($result.averageTimeMs) ms ($($result.performance))" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response.StatusCode -eq 400) {
        Write-Host "  Note: Vector search service may not be initialized" -ForegroundColor Yellow
    }
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Enhanced Vector Search Test Complete" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "New API Endpoints:" -ForegroundColor Yellow
Write-Host "  Available Strategies: $baseUrl/vectorsearchstrategy/available" -ForegroundColor White
Write-Host "  Current Strategy:     $baseUrl/vectorsearchstrategy/current" -ForegroundColor White
Write-Host "  Switch Strategy:      $baseUrl/vectorsearchstrategy/switch/{name}" -ForegroundColor White
Write-Host "  Auto-Select:          $baseUrl/vectorsearchstrategy/auto-select" -ForegroundColor White
Write-Host "  Performance Stats:    $baseUrl/vectorsearchstrategy/stats" -ForegroundColor White
Write-Host "  Benchmark:            $baseUrl/vectorsearchstrategy/benchmark" -ForegroundColor White
Write-Host "  Requirements:         $baseUrl/vectorsearchstrategy/requirements" -ForegroundColor White
Write-Host ""
