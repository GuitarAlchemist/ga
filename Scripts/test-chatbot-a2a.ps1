#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Smoke-test the GA chatbot A2A endpoint.
.DESCRIPTION
    Verifies the live A2A discovery card, synchronous message/send,
    streaming message/stream, and JSON-RPC error behavior against a running
    chatbot API.
.PARAMETER ApiUrl
    Base URL of the chatbot API.
.PARAMETER TimeoutSeconds
    Per-request timeout in seconds.
.EXAMPLE
    pwsh Scripts/test-chatbot-a2a.ps1
.EXAMPLE
    pwsh Scripts/test-chatbot-a2a.ps1 -ApiUrl http://localhost:5252 -TimeoutSeconds 120
#>

param(
    [string]$ApiUrl = "http://localhost:5252",
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-JsonRpc {
    param(
        [string]$Method,
        [object]$Params,
        [string]$Id = "a2a-smoke"
    )

    $body = @{
        jsonrpc = "2.0"
        id = $Id
        method = $Method
        params = $Params
    } | ConvertTo-Json -Depth 20

    Invoke-RestMethod `
        -Uri "$ApiUrl/a2a" `
        -Method Post `
        -Body $body `
        -ContentType "application/json" `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-A2AStream {
    param(
        [object]$Params,
        [string]$Id = "a2a-stream-smoke"
    )

    $body = @{
        jsonrpc = "2.0"
        id = $Id
        method = "message/stream"
        params = $Params
    } | ConvertTo-Json -Depth 20

    $response = Invoke-WebRequest `
        -Uri "$ApiUrl/a2a" `
        -Method Post `
        -Body $body `
        -ContentType "application/json" `
        -TimeoutSec $TimeoutSeconds

    Assert-True ($response.StatusCode -eq 200) "Expected message/stream HTTP 200."
    $contentType = [string]::Join(";", @($response.Headers["Content-Type"]))
    Assert-True ($contentType -like "text/event-stream*") "Expected text/event-stream content type."

    $response.Content `
        -split "`n" `
        | Where-Object { $_.StartsWith("data: ", [StringComparison]::Ordinal) } `
        | ForEach-Object { $_.Substring("data: ".Length).Trim() } `
        | Where-Object { $_ }
}

function New-MessageParams {
    param(
        [string]$Text,
        [string]$ContextId = "ctx-a2a-smoke"
    )

    @{
        message = @{
            role = "user"
            contextId = $ContextId
            parts = @(
                @{
                    kind = "text"
                    text = $Text
                }
            )
        }
    }
}

Write-Host "GA chatbot A2A smoke test" -ForegroundColor Cyan
Write-Host "API: $ApiUrl"

Write-Step "API metadata"
$metadata = Invoke-RestMethod -Uri "$ApiUrl/api" -Method Get -TimeoutSec 10
Assert-True ($metadata.service -eq "ga-chatbot-api") "Unexpected /api service value."
Write-Host "OK: $($metadata.service) $($metadata.version)" -ForegroundColor Green

Write-Step "Agent card"
$card = Invoke-RestMethod -Uri "$ApiUrl/.well-known/agent-card.json" -Method Get -TimeoutSec 10
Assert-True ($card.name -eq "Guitar Alchemist Chatbot") "Unexpected agent card name."
Assert-True ($card.url -eq "$ApiUrl/a2a") "Unexpected A2A URL '$($card.url)'."
Assert-True ($card.preferredTransport -eq "JSONRPC") "Agent card does not advertise JSONRPC."
Assert-True ([bool]$card.capabilities.streaming) "Agent card does not advertise streaming."
Assert-True ($card.skills.Count -gt 0) "Agent card has no skills."
Write-Host "OK: discovery card advertises JSON-RPC streaming" -ForegroundColor Green

Write-Step "message/send"
$send = Invoke-JsonRpc `
    -Method "message/send" `
    -Params (New-MessageParams "What notes are in C major?")
Assert-True ($send.jsonrpc -eq "2.0") "message/send did not return JSON-RPC 2.0."
Assert-True ($null -ne $send.result) "message/send did not return a result."
Assert-True ($send.result.kind -eq "message") "message/send did not return a message result."
Assert-True ($send.result.role -eq "agent") "message/send result role is not agent."
Assert-True ($send.result.parts.Count -gt 0) "message/send result has no parts."
Assert-True (($send.result.parts[0].text -match "C") -and ($send.result.parts[0].text -match "D")) "message/send answer did not mention expected C major notes."
Assert-True ($null -ne $send.result.metadata.trace) "message/send result has no agentic trace."
Assert-True ($send.result.metadata.trace.protocol -match "otel-genai") "message/send trace does not advertise OTel GenAI semantics."
Assert-True ($send.result.metadata.trace.steps.Count -gt 0) "message/send trace has no steps."
Assert-True ($send.result.metadata.trace.steps[0].name -eq "chat.request") "message/send trace does not start with chat.request."
Write-Host "OK: message/send returned an agent message" -ForegroundColor Green

Write-Step "message/send conversation context"
$context = Invoke-JsonRpc `
    -Method "message/send" `
    -Params (New-MessageParams "In my previous A2A message, which scale did I ask about? Reply with the scale name only.")
Assert-True ($context.result.parts.Count -gt 0) "Context follow-up returned no text parts."
Assert-True ($context.result.contextId -eq "ctx-a2a-smoke") "Context follow-up did not preserve contextId."
Assert-True ($context.result.metadata.historyTurnCount -ge 2) "Context follow-up did not receive prior conversation turns."
Write-Host "Answer: $($context.result.parts[0].text)"
Write-Host "OK: message/send preserved same-context conversation history" -ForegroundColor Green

Write-Step "message/stream"
$events = @(Invoke-A2AStream -Params (New-MessageParams "What notes are in C major?" "ctx-a2a-stream-smoke"))
Assert-True ($events.Count -ge 3) "Expected at least task, artifact, and final status events."

$jsonEvents = @($events | ForEach-Object { $_ | ConvertFrom-Json })
$results = @($jsonEvents | ForEach-Object { $_.result } | Where-Object { $null -ne $_ })
$task = $results | Where-Object { $_.kind -eq "task" } | Select-Object -First 1
$artifact = $results | Where-Object { $_.kind -eq "artifact-update" } | Select-Object -First 1
$final = $results | Where-Object { $_.kind -eq "status-update" -and $_.final -eq $true } | Select-Object -First 1

Assert-True ($null -ne $task) "message/stream did not emit an initial task."
Assert-True ($task.status.state -eq "working") "Initial task status was not working."
Assert-True ($null -ne $artifact) "message/stream did not emit an artifact update."
Assert-True ($artifact.artifact.parts.Count -gt 0) "Artifact update had no text parts."
Assert-True ($null -ne $final) "message/stream did not emit a final status update."
Assert-True ($final.status.state -eq "completed") "Final stream status was not completed."
Write-Host "OK: message/stream returned task, artifact, and completed status" -ForegroundColor Green

Write-Step "JSON-RPC invalid method"
$invalid = Invoke-JsonRpc -Method "tasks/get" -Params @{ id = "missing-task" } -Id "a2a-invalid-method"
Assert-True ($null -ne $invalid.error) "Invalid method did not return an error."
Assert-True ($invalid.error.code -eq -32601) "Invalid method did not return JSON-RPC -32601."
Write-Host "OK: invalid method returns JSON-RPC -32601" -ForegroundColor Green

Write-Host ""
Write-Host "A2A smoke test passed." -ForegroundColor Green
