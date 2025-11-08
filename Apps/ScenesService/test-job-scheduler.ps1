# Test script for the job scheduler system
# This script tests enqueueing jobs, checking status, and cancellation

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Job Scheduler Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "http://localhost:5190"
$sceneId = "test-job-$( Get-Random -Maximum 1000 )"

# Test scene data
$jobData = @{
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
}
)
} | ConvertTo-Json -Depth 10

Write-Host "Enqueueing build job for scene '$sceneId'..." -ForegroundColor Yellow

try {
# Enqueue the job
$enqueueResponse = Invoke-RestMethod -Uri "$baseUrl/jobs/enqueue" -Method Post -Body $jobData -ContentType "application/json"
$jobId = $enqueueResponse.jobId

Write-Host "✅ Job enqueued successfully!" -ForegroundColor Green
Write-Host "   Job ID: $jobId" -ForegroundColor Gray
Write-Host ""

# Poll job status
Write-Host "Polling job status..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 0

do {
Start-Sleep -Seconds 1
$attempt++

$job = Invoke-RestMethod -Uri "$baseUrl/jobs/$jobId" -Method Get
$status = $job.Status

Write-Host "   Attempt $attempt`: Status = $status" -ForegroundColor Gray

if ($status -eq "Succeeded") {
Write-Host "✅ Job completed successfully!" -ForegroundColor Green
Write-Host "   Started: $($job.StartedUtc)" -ForegroundColor Gray
Write-Host "   Completed: $($job.CompletedUtc)" -ForegroundColor Gray
Write-Host "   Attempts: $($job.Attempt)" -ForegroundColor Gray
break
}
elseif ($status -eq "Failed") {
Write-Host "❌ Job failed!" -ForegroundColor Red
Write-Host "   Error: $($job.Error)" -ForegroundColor Red
Write-Host "   Attempts: $($job.Attempt)/$($job.MaxAttempts)" -ForegroundColor Red
break
}
elseif ($status -eq "Canceled") {
Write-Host "⚠️ Job was canceled" -ForegroundColor Yellow
break
}

} while ($attempt -lt $maxAttempts -and $status -in @("Queued", "Running"))

if ($attempt -ge $maxAttempts) {
Write-Host "⚠️ Timeout waiting for job completion" -ForegroundColor Yellow
}

# If job succeeded, test the resulting scene
if ($status -eq "Succeeded") {
Write-Host ""
Write-Host "Testing generated scene..." -ForegroundColor Yellow

# Test GLB download
$glbUrl = "$baseUrl/scenes/$sceneId.glb"
$headers = Invoke-WebRequest -Uri $glbUrl -Method Head

Write-Host "✅ Generated GLB accessible!" -ForegroundColor Green
Write-Host "   Size: $($headers.Headers.'Content-Length') bytes" -ForegroundColor Gray
Write-Host "   ETag: $($headers.Headers.ETag)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Testing job listing..." -ForegroundColor Yellow
$jobs = Invoke-RestMethod -Uri "$baseUrl/jobs?take=5" -Method Get

Write-Host "✅ Recent jobs:" -ForegroundColor Green
foreach ($recentJob in $jobs) {
$statusColor = switch ($recentJob.Status) {
"Succeeded" {
"Green"
}
"Failed" {
"Red"
}
"Canceled" {
"Yellow"
}
default {
"Gray"
}
}
Write-Host "   $($recentJob.JobId): $($recentJob.SceneId) - $($recentJob.Status)" -ForegroundColor $statusColor
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Job Scheduler Test Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

} catch {
Write-Host "❌ Test failed!" -ForegroundColor Red
Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red

if ($_.Exception.Response) {
$statusCode = $_.Exception.Response.StatusCode
Write-Host "Status Code: $statusCode" -ForegroundColor Red
}
}
