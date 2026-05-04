#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Live QA smoke test for the local GA chatbot.
.DESCRIPTION
    Exercises the same HTTP surface as the mini UI and asserts the behavior that
    matters for interactive QA: readiness, a fast simple response, renderable
    VexTab for known voicings, and useful agentic trace steps.
#>

param(
    [string]$ApiUrl = "http://localhost:5252",
    [int]$SimpleTimeoutSeconds = 20,
    [int]$VoicingTimeoutSeconds = 240,
    [int]$MaxSimpleElapsedMs = 10000,
    [int]$MaxVoicingElapsedMs = 10000
)

$ErrorActionPreference = "Stop"

function Invoke-ChatbotPost {
    param(
        [string]$Message,
        [int]$TimeoutSeconds
    )

    $body = @{ message = $Message } | ConvertTo-Json
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-RestMethod `
        -Uri "$ApiUrl/api/chatbot/chat" `
        -Method Post `
        -ContentType "application/json" `
        -Body $body `
        -TimeoutSec $TimeoutSeconds
    $stopwatch.Stop()

    [pscustomobject]@{
        Response = $response
        WallClockMs = [int]$stopwatch.ElapsedMilliseconds
    }
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

Write-Host "GA chatbot live QA" -ForegroundColor Cyan
Write-Host "API: $ApiUrl"

$status = Invoke-RestMethod -Uri "$ApiUrl/api/chatbot/status" -TimeoutSec 10
Assert-True $status.isAvailable "Chatbot status is not available: $($status | ConvertTo-Json -Compress)"
Write-Host "OK: status available ($($status.message))" -ForegroundColor Green

$simple = Invoke-ChatbotPost -Message "What is C major?" -TimeoutSeconds $SimpleTimeoutSeconds
Assert-True ($simple.Response.naturalLanguageAnswer -match "C") "Simple answer did not mention C."
Assert-True ($simple.Response.elapsedMs -le $MaxSimpleElapsedMs) "Simple answer too slow: $($simple.Response.elapsedMs)ms."
Write-Host "OK: simple prompt $($simple.Response.elapsedMs)ms, route $($simple.Response.agentId)/$($simple.Response.routingMethod)" -ForegroundColor Green

$voicing = Invoke-ChatbotPost -Message "Show me Dm7 shell voicings" -TimeoutSeconds $VoicingTimeoutSeconds
$answer = [string]$voicing.Response.naturalLanguageAnswer
$traceSteps = @($voicing.Response.trace.steps)
$traceStepNames = @($traceSteps | ForEach-Object { $_.name })
$notationStep = @($traceSteps | Where-Object { $_.name -eq "notation.vextab" }) | Select-Object -First 1

Assert-True ($answer -match '```vextab') "Voicing answer did not include a fenced vextab block."
Assert-True ($answer -match "\d+/\d+") "Voicing answer did not include string/fret notation tokens."
Assert-True ($voicing.Response.routingMethod -eq "deterministic-voicing") "Voicing prompt did not use deterministic routing: $($voicing.Response.routingMethod)."
Assert-True ($voicing.Response.elapsedMs -le $MaxVoicingElapsedMs) "Voicing answer too slow: $($voicing.Response.elapsedMs)ms."
Assert-True ($traceStepNames -contains "orchestration.route") "Trace missing orchestration.route."
Assert-True ($traceStepNames -contains "agent.semantic_result") "Trace missing agent.semantic_result."
Assert-True ($traceStepNames -contains "notation.vextab") "Trace missing notation.vextab."
Assert-True ($traceStepNames -contains "response.emit") "Trace missing response.emit."
Assert-True ($null -ne $notationStep) "Trace notation step was not found."
Assert-True ([int]$notationStep.attributes.'notation.diagram.count' -ge 1) "Trace did not report detected chord diagrams."

Write-Host "OK: voicing prompt $($voicing.Response.elapsedMs)ms, vextab generated, trace steps=$($traceSteps.Count)" -ForegroundColor Green

[pscustomobject]@{
    Status = "passed"
    ApiUrl = $ApiUrl
    Simple = [pscustomobject]@{
        AgentId = $simple.Response.agentId
        RoutingMethod = $simple.Response.routingMethod
        ElapsedMs = $simple.Response.elapsedMs
        WallClockMs = $simple.WallClockMs
    }
    Voicing = [pscustomobject]@{
        AgentId = $voicing.Response.agentId
        RoutingMethod = $voicing.Response.routingMethod
        ElapsedMs = $voicing.Response.elapsedMs
        WallClockMs = $voicing.WallClockMs
        VexTabCount = ([regex]::Matches($answer, '```vextab')).Count
        TraceStepCount = $traceSteps.Count
        TraceSteps = $traceStepNames
    }
} | ConvertTo-Json -Depth 6

Write-Host "Chatbot live QA passed." -ForegroundColor Green
