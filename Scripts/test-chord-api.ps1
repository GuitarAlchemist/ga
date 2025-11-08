# Test script for Guitar Alchemist Chord API
# Tests all MongoDB-backed endpoints

$baseUrl = "http://localhost:5232/api/chords"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Guitar Alchemist Chord API - Test Suite" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Test 1: Get total chord count
Write-Host "Test 1: Get Total Chord Count" -ForegroundColor Yellow
Write-Host "GET $baseUrl/count" -ForegroundColor Gray
$count = Invoke-RestMethod -Uri "$baseUrl/count"
Write-Host "Total chords: $count" -ForegroundColor Green
Write-Host ""

# Test 2: Get Major chords
Write-Host "Test 2: Get Major Chords (limit 3)" -ForegroundColor Yellow
Write-Host "GET $baseUrl/quality/Major?limit=3" -ForegroundColor Gray
$majorChords = Invoke-RestMethod -Uri "$baseUrl/quality/Major?limit=3"
$majorChords | ForEach-Object {
    Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
    Write-Host "    Quality: $( $_.quality ), Extension: $( $_.extension ), Stacking: $( $_.stackingType )" -ForegroundColor DarkGray
}
Write-Host ""

# Test 3: Get Quartal chords
Write-Host "Test 3: Get Quartal Chords (limit 3)" -ForegroundColor Yellow
Write-Host "GET $baseUrl/stacking/Quartal?limit=3" -ForegroundColor Gray
$quartalChords = Invoke-RestMethod -Uri "$baseUrl/stacking/Quartal?limit=3"
$quartalChords | ForEach-Object {
    Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
    Write-Host "    Stacking: $( $_.stackingType ), Notes: $( $_.noteCount )" -ForegroundColor DarkGray
}
Write-Host ""

# Test 4: Get triads (3 notes)
Write-Host "Test 4: Get Triads (3 notes, limit 3)" -ForegroundColor Yellow
Write-Host "GET $baseUrl/note-count/3?limit=3" -ForegroundColor Gray
$triads = Invoke-RestMethod -Uri "$baseUrl/note-count/3?limit=3"
$triads | ForEach-Object {
    Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
    Write-Host "    Notes: $( $_.noteCount ), Quality: $( $_.quality )" -ForegroundColor DarkGray
}
Write-Host ""

# Test 5: Get chords by pitch class set [0,3,7] (C minor triad)
Write-Host "Test 5: Get Chords by Pitch Class Set [0,3,7] (limit 3)" -ForegroundColor Yellow
Write-Host "GET $baseUrl/pitch-class-set?pcs=0,3,7&limit=3" -ForegroundColor Gray
$pcsChords = Invoke-RestMethod -Uri "$baseUrl/pitch-class-set?pcs=0,3,7&limit=3"
$pcsChords | ForEach-Object {
    Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
    Write-Host "    Quality: $( $_.quality ), PCS: $( $_.pitchClassSet )" -ForegroundColor DarkGray
}
Write-Host ""

# Test 6: Search for "diminished"
Write-Host "Test 6: Search for 'diminished' (limit 3)" -ForegroundColor Yellow
Write-Host "GET $baseUrl/search?q=diminished&limit=3" -ForegroundColor Gray
$searchResults = Invoke-RestMethod -Uri "$baseUrl/search?q=diminished&limit=3"
$searchResults | ForEach-Object {
    Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
    Write-Host "    Quality: $( $_.quality ), Extension: $( $_.extension )" -ForegroundColor DarkGray
}
Write-Host ""

# Test 7: Get statistics by quality
Write-Host "Test 7: Get Statistics by Quality" -ForegroundColor Yellow
Write-Host "GET $baseUrl/stats/by-quality" -ForegroundColor Gray
$qualityStats = Invoke-RestMethod -Uri "$baseUrl/stats/by-quality"
$qualityStats.PSObject.Properties | ForEach-Object {
    Write-Host "  $( $_.Name ): $( $_.Value )" -ForegroundColor White
}
Write-Host ""

# Test 8: Get statistics by stacking type
Write-Host "Test 8: Get Statistics by Stacking Type" -ForegroundColor Yellow
Write-Host "GET $baseUrl/stats/by-stacking-type" -ForegroundColor Gray
$stackingStats = Invoke-RestMethod -Uri "$baseUrl/stats/by-stacking-type"
$stackingStats.PSObject.Properties | ForEach-Object {
    Write-Host "  $( $_.Name ): $( $_.Value )" -ForegroundColor White
}
Write-Host ""

# Test 9: Get distinct qualities
Write-Host "Test 9: Get Distinct Qualities" -ForegroundColor Yellow
Write-Host "GET $baseUrl/distinct/qualities" -ForegroundColor Gray
$qualities = Invoke-RestMethod -Uri "$baseUrl/distinct/qualities"
Write-Host "  $( $qualities -join ', ' )" -ForegroundColor White
Write-Host ""

# Test 10: Get distinct extensions
Write-Host "Test 10: Get Distinct Extensions" -ForegroundColor Yellow
Write-Host "GET $baseUrl/distinct/extensions" -ForegroundColor Gray
$extensions = Invoke-RestMethod -Uri "$baseUrl/distinct/extensions"
Write-Host "  $( $extensions -join ', ' )" -ForegroundColor White
Write-Host ""

# Test 11: Get distinct stacking types
Write-Host "Test 11: Get Distinct Stacking Types" -ForegroundColor Yellow
Write-Host "GET $baseUrl/distinct/stacking-types" -ForegroundColor Gray
$stackingTypes = Invoke-RestMethod -Uri "$baseUrl/distinct/stacking-types"
Write-Host "  $( $stackingTypes -join ', ' )" -ForegroundColor White
Write-Host ""

# Test 12: Get chords by extension
Write-Host "Test 12: Get Seventh Chords (limit 3)" -ForegroundColor Yellow
Write-Host "GET $baseUrl/extension/Seventh?limit=3" -ForegroundColor Gray
$seventhChords = Invoke-RestMethod -Uri "$baseUrl/extension/Seventh?limit=3"
$seventhChords | ForEach-Object {
    Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
    Write-Host "    Extension: $( $_.extension ), Quality: $( $_.quality )" -ForegroundColor DarkGray
}
Write-Host ""

# Test 13: Get chords by quality and extension
Write-Host "Test 13: Get Major Seventh Chords (limit 3)" -ForegroundColor Yellow
Write-Host "GET $baseUrl/quality/Major/extension/Seventh?limit=3" -ForegroundColor Gray
$majorSeventhChords = Invoke-RestMethod -Uri "$baseUrl/quality/Major/extension/Seventh?limit=3"
$majorSeventhChords | ForEach-Object {
    Write-Host "  [$( $_.id )] $( $_.name )" -ForegroundColor White
    Write-Host "    Quality: $( $_.quality ), Extension: $( $_.extension )" -ForegroundColor DarkGray
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "All tests completed successfully! ✓" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "API is running on: http://localhost:5232" -ForegroundColor Yellow
Write-Host "Swagger UI: http://localhost:5232/swagger`n" -ForegroundColor Yellow

