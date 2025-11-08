# Test script to build a scene using the ScenesService API
# This script creates a test scene with two rooms and a portal

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ScenesService API Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "http://localhost:5190"
$sceneId = "test01"

# Test scene data - hall and kitchen with portal
$sceneData = @{
sceneId = $sceneId
cells = @(
@{
cellId = "hall"
meshes = @(
@{
meshId = "auto"
}
)
},
@{
cellId = "kitchen"
meshes = @(
@{
meshId = "auto"
}
)
}
)
portals = @(
@{
from = "hall"
to = "kitchen"
quad = @(2.0, 0.2, -0.6, 2.0, 2.2, -0.6, 2.0, 2.2, 0.6, 2.0, 0.2, 0.6)
},
@{
from = "kitchen"
to = "hall"
quad = @(2.5, 0.2, -0.6, 2.5, 2.2, -0.6, 2.5, 2.2, 0.6, 2.5, 0.2, 0.6)
}
)
} | ConvertTo-Json -Depth 10

Write-Host "Building scene '$sceneId'..." -ForegroundColor Yellow

try {
# Build the scene
$response = Invoke-RestMethod -Uri "$baseUrl/scenes/build" -Method Post -Body $sceneData -ContentType "application/json"

Write-Host "✅ Scene built successfully!" -ForegroundColor Green
Write-Host "   Scene ID: $($response.SceneId)" -ForegroundColor Gray
Write-Host "   ETag: $($response.ETag)" -ForegroundColor Gray
Write-Host "   Size: $($response.Bytes) bytes" -ForegroundColor Gray
Write-Host ""

# Test downloading the GLB
Write-Host "Testing GLB download..." -ForegroundColor Yellow
$glbUrl = "$baseUrl/scenes/$sceneId.glb"

# Get headers first
$headers = Invoke-WebRequest -Uri $glbUrl -Method Head
Write-Host "✅ GLB accessible!" -ForegroundColor Green
Write-Host "   Content-Type: $($headers.Headers.'Content-Type')" -ForegroundColor Gray
Write-Host "   ETag: $($headers.Headers.ETag)" -ForegroundColor Gray
Write-Host "   Content-Length: $($headers.Headers.'Content-Length') bytes" -ForegroundColor Gray
Write-Host ""

# Test metadata
Write-Host "Testing metadata..." -ForegroundColor Yellow
$metaUrl = "$baseUrl/scenes/$sceneId/meta"
$meta = Invoke-RestMethod -Uri $metaUrl -Method Get

Write-Host "✅ Metadata accessible!" -ForegroundColor Green
Write-Host "   Metadata:" -ForegroundColor Gray
Write-Host ($meta | ConvertTo-Json -Depth 5) -ForegroundColor Gray
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Test Complete - All Systems Working!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "You can now:" -ForegroundColor White
Write-Host "  • Load the scene in Three.js: $glbUrl" -ForegroundColor Yellow
Write-Host "  • View metadata: $metaUrl" -ForegroundColor Yellow
Write-Host "  • Use in Godot 4 or other engines" -ForegroundColor Yellow

} catch {
Write-Host "❌ Test failed!" -ForegroundColor Red
Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red

if ($_.Exception.Response) {
$statusCode = $_.Exception.Response.StatusCode
Write-Host "Status Code: $statusCode" -ForegroundColor Red

try {
$errorContent = $_.Exception.Response.GetResponseStream()
$reader = New-Object System.IO.StreamReader($errorContent)
$errorText = $reader.ReadToEnd()
Write-Host "Response: $errorText" -ForegroundColor Red
} catch {
Write-Host "Could not read error response" -ForegroundColor Red
}
}
}
