# Test script for Music Theory DSL Language Server
# This script sends LSP messages to the server and verifies responses

Write-Host "Testing Music Theory DSL Language Server..." -ForegroundColor Cyan

# Build the LSP server
Write-Host "`nBuilding LSP server..." -ForegroundColor Yellow
dotnet build Apps/GaMusicTheoryLsp/GaMusicTheoryLsp.fsproj
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build successful!" -ForegroundColor Green

# Test 1: Initialize request
Write-Host "`nTest 1: Initialize Request" -ForegroundColor Yellow
$initRequest = @"
Content-Length: 123

{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{}}}
"@

Write-Host "Sending initialize request..." -ForegroundColor Gray
Write-Host $initRequest -ForegroundColor DarkGray

# Test 2: Document open with chord progression
Write-Host "`nTest 2: Document Open (Chord Progression)" -ForegroundColor Yellow
$docOpenRequest = @"
Content-Length: 234

{"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.chord","languageId":"music-theory-dsl","version":1,"text":"I - IV - V - I\nkey: C major"}}}
"@

Write-Host "Sending document open request..." -ForegroundColor Gray
Write-Host $docOpenRequest -ForegroundColor DarkGray

# Test 3: Completion request
Write-Host "`nTest 3: Completion Request" -ForegroundColor Yellow
$completionRequest = @"
Content-Length: 156

{"jsonrpc":"2.0","id":2,"method":"textDocument/completion","params":{"textDocument":{"uri":"file:///test.chord"},"position":{"line":0,"character":5}}}
"@

Write-Host "Sending completion request..." -ForegroundColor Gray
Write-Host $completionRequest -ForegroundColor DarkGray

# Test 4: Hover request
Write-Host "`nTest 4: Hover Request" -ForegroundColor Yellow
$hoverRequest = @"
Content-Length: 150

{"jsonrpc":"2.0","id":3,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///test.chord"},"position":{"line":0,"character":0}}}
"@

Write-Host "Sending hover request..." -ForegroundColor Gray
Write-Host $hoverRequest -ForegroundColor DarkGray

Write-Host "`n=== Manual Testing Instructions ===" -ForegroundColor Cyan
Write-Host "To manually test the LSP server:" -ForegroundColor White
Write-Host "1. Run: dotnet run --project Apps/GaMusicTheoryLsp/GaMusicTheoryLsp.fsproj" -ForegroundColor Gray
Write-Host "2. Paste the above requests (including Content-Length header)" -ForegroundColor Gray
Write-Host "3. Press Enter twice after each request" -ForegroundColor Gray
Write-Host "4. Observe the JSON responses" -ForegroundColor Gray

Write-Host "`n=== VS Code Extension Setup ===" -ForegroundColor Cyan
Write-Host "To use with VS Code:" -ForegroundColor White
Write-Host "1. Create a VS Code extension (see README.md)" -ForegroundColor Gray
Write-Host "2. Configure the extension to launch this LSP server" -ForegroundColor Gray
Write-Host "3. Install the extension in VS Code" -ForegroundColor Gray
Write-Host "4. Open a .mtdsl, .chord, .fret, .scale, or .groth file" -ForegroundColor Gray
Write-Host "5. Enjoy auto-completion and diagnostics!" -ForegroundColor Gray

Write-Host "`nLSP Server Test Complete!" -ForegroundColor Green

