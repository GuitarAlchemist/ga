# AgentTree.test.ps1 — deterministic tests. ALL dispatch here is SIMULATED (injected sender); no network.
#   pwsh Scripts/agent-tree-r1/AgentTree.test.ps1
[CmdletBinding()] param()
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AgentTree.psm1') -Force
$failures = @()
function Assert { param([bool]$C, [string]$M)
    if ($C) { Write-Host "  ok: $M" -ForegroundColor Green } else { $script:failures += $M; Write-Host "  FAIL: $M" -ForegroundColor Red } }
function Leaf { param($T, $Id, $Parent = 'root', $Mode = 'read', $Scope = $null)
    Add-AgentNode -Tree $T -NodeId $Id -ParentId $Parent -Scope ($Scope ?? "scope-$Id") -Objective "obj $Id" -Mode $Mode -Baseline 4 -Target 0 -BaseSha 'abc' }
$okSender = { param($n, $k) @{ comment_url = "https://example.invalid/sim/comment/$k" } }
$badSender = { param($n, $k) throw 'timeout' }

Write-Host 'dry-run / drafted != launched'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'
$r = Invoke-AgentDispatch -Tree $t -NodeId 'a'
Assert ($r.ok -and -not $r.launched -and $t.executions -eq 0 -and $t.nodes['a'].status -eq 'pending') 'default dry-run drafts only, spends no execution, node stays pending'

Write-Host 'dispatch, correlation, self-report is not acceptance' 
$r = Invoke-AgentDispatch -Tree $t -NodeId 'a' -Live -Sender $okSender
Assert ($r.launched -and $t.nodes['a'].status -eq 'dispatched' -and -not $t.nodes['a'].correlated) 'live dispatch records receipt but not correlation'
$x = Submit-ChildReport -Tree $t -NodeId 'a' -Result success -Evidence 'e1'
Assert (-not $x.ok -and $x.reason -eq 'uncorrelated_report') 'report refused before run correlation'
Assert (-not (Confirm-AgentRun -Tree $t -NodeId 'a' -RunUrl 'https://example.invalid/run/1').ok) 'correlation needs run AND response URL'
$null = Confirm-AgentRun -Tree $t -NodeId 'a' -RunUrl 'https://example.invalid/run/1' -ResponseUrl 'https://example.invalid/resp/1'
$null = Submit-ChildReport -Tree $t -NodeId 'a' -Result success -Evidence 'e1' -After 1
Assert ($t.nodes['a'].status -eq 'reported' -and (Get-TreeGap $t).accepted_nodes -eq 0) 'self-report leaves node "reported", gap unchanged'
Assert (-not (Set-ParentVerdict -Tree $t -NodeId 'a' -Actor 'someone-else' -Verdict accept).ok) 'non-parent cannot accept'
Assert ((Set-ParentVerdict -Tree $t -NodeId 'a' -Actor 'root' -Verdict accept).ok -and $t.nodes['a'].status -eq 'accepted') 'immediate parent accepts'
$g = Get-TreeGap $t; Assert ($g.before -eq 8 -or $g.before -eq 4) 'gap aggregate computed'
Assert ($g.after -lt $g.before) 'accepted node reduces aggregate gap'

Write-Host 'failure / blocked / unknown distinctions'
$t = New-AgentTree -RootId 'root' -Limits @{ MaxConcurrent = 5; MaxExecutions = 5 }; foreach ($id in 'f', 'b', 'u') { $null = Leaf $t $id; $null = Invoke-AgentDispatch -Tree $t -NodeId $id -Live -Sender $okSender
    $null = Confirm-AgentRun $t $id 'r' 's' }
$null = Submit-ChildReport $t 'f' failure; $null = Submit-ChildReport $t 'b' blocked; $null = Submit-ChildReport $t 'u' unknown
Assert (-not (Set-ParentVerdict $t 'f' 'root' accept).ok) 'failure cannot be accepted'
$null = Set-ParentVerdict $t 'f' 'root' reject; $null = Set-ParentVerdict $t 'b' 'root' reject
Assert ($t.nodes['f'].status -eq 'failed' -and $t.nodes['b'].status -eq 'blocked' -and $t.nodes['u'].status -eq 'reported') 'failed/blocked/unknown stay distinct'

Write-Host 'retry budget'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'
$null = Invoke-AgentDispatch $t 'a' -Live -Sender $okSender; $null = Confirm-AgentRun $t 'a' 'r' 's'; $null = Submit-ChildReport $t 'a' failure
Assert (-not (Set-ParentVerdict $t 'a' 'root' retry).ok) 'retry without a different approach refused'
Assert ((Set-ParentVerdict $t 'a' 'root' retry -NewApproach 'narrower scope').ok) 'retry with new approach allowed (attempt 2)'
$null = Invoke-AgentDispatch $t 'a' -Live -Sender $okSender; $null = Confirm-AgentRun $t 'a' 'r2' 's2'; $null = Submit-ChildReport $t 'a' failure
Assert ((Set-ParentVerdict $t 'a' 'root' retry -NewApproach 'again').reason -eq 'attempt_limit') 'third attempt refused (max 2)'

Write-Host 'duplicate / uncertain delivery'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'
$r = Invoke-AgentDispatch $t 'a' -Live -Sender $badSender
Assert ($r.reason -eq 'uncertain' -and $t.nodes['a'].status -eq 'uncertain' -and $t.executions -eq 1) 'sender failure => uncertain, execution still counted'
Assert ((Invoke-AgentDispatch $t 'a' -Live -Sender $okSender).reason -eq 'uncertain_dispatch_no_replay') 'uncertain dispatch never replayed'
Assert ((Get-IdempotencyKey $t.nodes['a'] 1) -eq (Get-IdempotencyKey $t.nodes['a'] 1)) 'idempotency key is stable'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'; $null = Invoke-AgentDispatch $t 'a' -Live -Sender $okSender
Assert ((Invoke-AgentDispatch $t 'a' -Live -Sender $okSender).reason -eq 'bad_state_dispatched') 'second dispatch of in-flight node refused'

Write-Host 'bounded expansion'
$t = New-AgentTree -RootId 'root'
Assert ((Leaf $t 'c1').ok -and (Leaf $t 'c2').ok) 'two concurrent children allowed'
Assert ((Leaf $t 'c3').reason -eq 'concurrency_limit') 'third concurrent child refused'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'c1'; $null = Leaf $t 'c2' ; $null = Leaf $t 'g1' 'c1'; $null = Leaf $t 'g2' 'c1'
Assert ((Leaf $t 'x' 'g1').reason -eq 'depth_limit') 'depth 4 refused (max 3)'
$t = New-AgentTree -RootId 'root' -Limits @{ MaxConcurrent = 99; MaxDepth = 99 }
1..6 | ForEach-Object { $null = Leaf $t "n$_" }
Assert ((Leaf $t 'n7').reason -eq 'node_limit' -and $t.nodes.Count -eq 7) '7-node cumulative cap (root included)'
$t = New-AgentTree -RootId 'root' -Limits @{ MaxConcurrent = 99 }
foreach ($i in 1..4) { $null = Leaf $t "e$i"; $null = Invoke-AgentDispatch $t "e$i" -Live -Sender $okSender }
Assert ($t.executions -eq 3 -and $t.nodes['e4'].status -eq 'pending') 'only 3 live executions total across the tree'

Write-Host 'permissions, writers'
$t = New-AgentTree -RootId 'root'
$null = Add-AgentNode $t 'ro' 'root' 's1' 'o' -Mode read -Permissions 'read'
Assert ((Add-AgentNode $t 'w' 'ro' 's2' 'o' -Mode write).reason -eq 'write_not_permitted') 'child cannot gain write from a read-only parent'
$t = New-AgentTree -RootId 'root' -Limits @{ MaxConcurrent = 5 }
Assert ((Add-AgentNode $t 'w1' 'root' 'branch' 'o' -Mode write).ok -and (Add-AgentNode $t 'w2' 'root' 'branch' 'o' -Mode write).reason -eq 'duplicate_writer') 'one writer per scope'

Write-Host 'cycle rejection / reparenting / escalation'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'; $null = Leaf $t 'b' 'a'
Assert ((Move-AgentNode $t 'a' 'b' 'root').reason -eq 'cycle') 'reparent under own descendant rejected'
Assert ((Move-AgentNode $t 'a' 'a' 'root').reason -eq 'cycle') 'self-parent rejected'
Assert ((Move-AgentNode $t 'b' 'root' 'a').reason -eq 'root_mediation_required') 'only root may reparent'
$null = Leaf $t 'c'; $m = Move-AgentNode $t 'b' 'c' 'root'
Assert ($m.ok -and $t.nodes['b'].parent_id -eq 'c' -and ($t.nodes['b'].ancestry -join ' ') -match 'was child of a') 'root reparents, ancestry history preserved'
$null = Invoke-AgentDispatch $t 'b' -Live -Sender $okSender; $null = Confirm-AgentRun $t 'b' 'r' 's'; $null = Submit-ChildReport $t 'b' blocked
$e = Set-ParentVerdict $t 'b' 'c' escalate -Reason 'need broader read access'
Assert ($e.ok -and $t.nodes['b'].status -eq 'escalated' -and -not $t.nodes['b'].escalation.grants_privileges) 'upward escalation is a proposal, grants nothing'
Assert ($t.nodes['b'].permissions.Count -le 2) 'escalation did not widen permissions'

Write-Host 'inbound requests'
$t = New-AgentTree -RootId 'root' -AllowedSenders 'owner'; $null = Leaf $t 'a'
Assert ((Test-InboundRequest $t @{ sender = 'owner'; node_id = 'a'; nonce = 'n1'; kind = 'report' }).ok) 'valid inbound accepted'
Assert ((Test-InboundRequest $t @{ sender = 'owner'; node_id = 'a'; nonce = 'n1'; kind = 'report' }).reason -eq 'replayed') 'replayed nonce rejected'
Assert ((Test-InboundRequest $t @{ sender = 'mallory'; node_id = 'a'; nonce = 'n2'; kind = 'report' }).reason -eq 'unauthorized') 'unauthorized sender rejected'
Assert ((Test-InboundRequest $t @{ sender = 'owner'; nonce = 'n3' }).reason -eq 'malformed') 'malformed rejected'

Write-Host 'stop switch / wall clock'
$t = New-AgentTree -RootId 'root'; Set-TreeStop $t
Assert ((Leaf $t 'a').reason -eq 'stop_switch') 'stop switch blocks node creation'
$t = New-AgentTree -RootId 'root' -Now ([datetime]::UtcNow.AddMinutes(-31))
Assert ((Leaf $t 'a').reason -eq 'wall_clock_exceeded') '30-minute wall clock enforced'

Write-Host 'independent QA'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'
Assert ((Request-IndependentQA $t 'a' 'qa-1' $null).qa -eq 'pending') 'no starter => QA stays pending'
Assert ((Request-IndependentQA $t 'a' 'root' { @{ verdict = 'pass' } }).qa -eq 'pending') 'root cannot self-certify'
Assert ((Request-IndependentQA $t 'a' 'qa-1' { throw 'cannot start' }).qa -eq 'pending') 'failed QA start => pending'
Assert ((Request-IndependentQA $t 'a' 'qa-1' { @{ verdict = 'pass' } }).qa -eq 'pass') 'separate identity QA recorded (simulated)'

Write-Host 'persistence'
$tmp = New-TemporaryFile; Save-AgentTree $t $tmp.FullName
Assert ((Get-Content $tmp -Raw | ConvertFrom-Json).schema -eq 'agent-tree-r1/v1') 'tree serialises to JSON'; Remove-Item $tmp

if ($failures.Count) { Write-Host "`n$($failures.Count) FAILED" -ForegroundColor Red; exit 1 }
Write-Host "`nAll agent-tree-r1 tests passed (simulated dispatch only)." -ForegroundColor Green
