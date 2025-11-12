# Test script for Voicing Search API
# Tests all endpoints with various queries and filters

$baseUrl = "http://localhost:5232"

Write-Host "=== Testing Voicing Search API ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Basic semantic search
Write-Host "Test 1: Basic semantic search - 'easy jazz chord'" -ForegroundColor Yellow
$response1 = Invoke-RestMethod -Uri "$baseUrl/api/voicings/search?q=easy jazz chord&limit=5" -Method Get
Write-Host "Found $($response1.results.Count) results" -ForegroundColor Green
Write-Host "First result: $($response1.results[0].name) (Score: $($response1.results[0].score))" -ForegroundColor Green
Write-Host ""

# Test 2: Search with difficulty filter
Write-Host "Test 2: Search with difficulty filter - 'major chord' (beginner)" -ForegroundColor Yellow
$response2 = Invoke-RestMethod -Uri "$baseUrl/api/voicings/search?q=major chord&difficulty=beginner&limit=5" -Method Get
Write-Host "Found $($response2.results.Count) results" -ForegroundColor Green
Write-Host "First result: $($response2.results[0].name) (Score: $($response2.results[0].score))" -ForegroundColor Green
Write-Host ""

# Test 3: Search with position filter
Write-Host "Test 3: Search with position filter - 'chord' (open position)" -ForegroundColor Yellow
$response3 = Invoke-RestMethod -Uri "$baseUrl/api/voicings/search?q=chord&position=open&limit=5" -Method Get
Write-Host "Found $($response3.results.Count) results" -ForegroundColor Green
Write-Host "First result: $($response3.results[0].name) (Score: $($response3.results[0].score))" -ForegroundColor Green
Write-Host ""

# Test 4: Get stats
Write-Host "Test 4: Get search statistics" -ForegroundColor Yellow
$stats = Invoke-RestMethod -Uri "$baseUrl/api/voicings/stats" -Method Get
Write-Host "Total voicings: $($stats.totalVoicings)" -ForegroundColor Green
Write-Host "Memory usage: $($stats.memoryUsageMB) MB" -ForegroundColor Green
Write-Host "Average search time: $($stats.averageSearchTimeMs) ms" -ForegroundColor Green
Write-Host "Strategy: $($stats.strategyName)" -ForegroundColor Green
Write-Host ""

# Test 5: Find similar voicings (if we have an ID from previous results)
if ($response1.results.Count -gt 0) {
    $firstId = $response1.results[0].id
    Write-Host "Test 5: Find similar voicings to ID: $firstId" -ForegroundColor Yellow
    $response5 = Invoke-RestMethod -Uri "$baseUrl/api/voicings/similar/$firstId?limit=5" -Method Get
    Write-Host "Found $($response5.results.Count) similar voicings" -ForegroundColor Green
    if ($response5.results.Count -gt 0) {
        Write-Host "First similar: $($response5.results[0].name) (Score: $($response5.results[0].score))" -ForegroundColor Green
    }
    Write-Host ""
}

# Test 6: Complex query with multiple filters
Write-Host "Test 6: Complex query - 'seventh chord' (intermediate, 5th position)" -ForegroundColor Yellow
$response6 = Invoke-RestMethod -Uri "$baseUrl/api/voicings/search?q=seventh chord&difficulty=intermediate&position=5&limit=5" -Method Get
Write-Host "Found $($response6.results.Count) results" -ForegroundColor Green
if ($response6.results.Count -gt 0) {
    Write-Host "First result: $($response6.results[0].name) (Score: $($response6.results[0].score))" -ForegroundColor Green
}
Write-Host ""

# Test 7: Different semantic queries
Write-Host "Test 7: Various semantic queries" -ForegroundColor Yellow
$queries = @(
    "warm sounding chord",
    "bright voicing",
    "dark minor chord",
    "open string chord",
    "barre chord"
)

foreach ($query in $queries) {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/voicings/search?q=$query&limit=3" -Method Get
    Write-Host "  '$query': $($response.results.Count) results" -ForegroundColor Gray
}
Write-Host ""

Write-Host "=== All tests completed successfully! ===" -ForegroundColor Cyan

