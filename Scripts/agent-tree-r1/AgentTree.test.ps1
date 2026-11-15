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
$t = New-AgentTree -RootId 'root' -UnsafeAllowWidenLimitsForTests -Limits @{ MaxConcurrent = 5; MaxExecutions = 5 }; foreach ($id in 'f', 'b', 'u') { $null = Leaf $t $id; $null = Invoke-AgentDispatch -Tree $t -NodeId $id -Live -Sender $okSender
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
$t = New-AgentTree -RootId 'root' -UnsafeAllowWidenLimitsForTests -Limits @{ MaxConcurrent = 99; MaxDepth = 99 }
1..6 | ForEach-Object { $null = Leaf $t "n$_" }
Assert ((Leaf $t 'n7').reason -eq 'node_limit' -and $t.nodes.Count -eq 7) '7-node cumulative cap (root included)'
$t = New-AgentTree -RootId 'root' -UnsafeAllowWidenLimitsForTests -Limits @{ MaxConcurrent = 99 }
foreach ($i in 1..4) { $null = Leaf $t "e$i"; $null = Invoke-AgentDispatch $t "e$i" -Live -Sender $okSender }
Assert ($t.executions -eq 3 -and $t.nodes['e4'].status -eq 'pending') 'only 3 live executions total across the tree'

Write-Host 'concurrency is tree-wide at dispatch (#855: at most 2 concurrent children, cumulative)'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'p1'; $null = Leaf $t 'p2'; $null = Leaf $t 'q1' 'p1'; $null = Leaf $t 'q2' 'p2'; $null = Leaf $t 'q3' 'p2'
$null = Invoke-AgentDispatch $t 'q1' -Live -Sender $okSender; $null = Invoke-AgentDispatch $t 'q2' -Live -Sender $okSender
$r = Invoke-AgentDispatch $t 'q3' -Live -Sender $okSender
Assert ($r.reason -eq 'concurrency_limit' -and $t.executions -eq 2 -and $t.nodes['q3'].status -eq 'pending' -and $t.nodes['q3'].attempts -eq 0) 'third in-flight child anywhere in the tree is refused before send; no budget spent'
# QA finding (run 43650162072): a -Simulated report must not release the slot of a node that has a live receipt.
Assert ((Submit-ChildReport -Tree $t -NodeId 'q1' -Result unknown -Simulated).reason -eq 'simulated_report_on_live_node' -and $t.nodes['q1'].status -eq 'dispatched') 'simulated report on a live-dispatched node is refused and keeps its slot'
Assert ((Invoke-AgentDispatch $t 'q3' -Live -Sender $okSender).reason -eq 'concurrency_limit') 'so a third live child is still refused'
$null = Confirm-AgentRun -Tree $t -NodeId 'q1' -RunUrl 'https://example.invalid/sim/run' -ResponseUrl 'https://example.invalid/sim/resp'
$null = Submit-ChildReport -Tree $t -NodeId 'q1' -Result success -Evidence 'e1'
Assert ((Invoke-AgentDispatch $t 'q3' -Live -Sender $okSender).launched -eq $true) 'a slot frees once an in-flight child has reported'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'u1'; $null = Leaf $t 'u2'; $null = Leaf $t 'u3' 'u1'
$null = Invoke-AgentDispatch $t 'u1' -Live -Sender $badSender; $null = Invoke-AgentDispatch $t 'u2' -Live -Sender $okSender
Assert ((Invoke-AgentDispatch $t 'u3' -Live -Sender $okSender).reason -eq 'concurrency_limit') 'an uncertain dispatch still occupies a slot (it may be running)'

Write-Host 'permissions, writers'
$t = New-AgentTree -RootId 'root'
$null = Add-AgentNode $t 'ro' 'root' 's1' 'o' -Mode read -Permissions 'read'
Assert ((Add-AgentNode $t 'w' 'ro' 's2' 'o' -Mode write).reason -eq 'write_not_permitted') 'child cannot gain write from a read-only parent'
$t = New-AgentTree -RootId 'root' -UnsafeAllowWidenLimitsForTests -Limits @{ MaxConcurrent = 5 }
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

Write-Host 'behavioural gap headline (child pwsh; injected check scripts, real tests untouched)'
$inj = Join-Path ([IO.Path]::GetTempPath()) ("agent-tree-gap-" + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $inj
$passS = Join-Path $inj 'pass.ps1'; $failS = Join-Path $inj 'fail.ps1'; $crashS = Join-Path $inj 'crash.ps1'
Set-Content -LiteralPath $passS -Value 'Write-Host "  ok: injected"; exit 0'
Set-Content -LiteralPath $failS -Value 'Write-Host "  ok: a"; Write-Host "  FAIL: injected check one"; Write-Host "  FAIL: injected check two"; exit 1'
Set-Content -LiteralPath $crashS -Value 'throw "boom"'
$bp = Measure-BehaviouralGap -TestScript $passS
Assert ($bp.gap -eq 0 -and $bp.exit_code -eq 0 -and $bp.label -eq 'behavioural') 'headline is 0 when the child run has no failing assertion'
$bf = Measure-BehaviouralGap -TestScript $failS
Assert ($bf.gap -eq 2 -and $bf.failing_assertions -eq 2 -and $bf.exit_code -eq 1) 'headline becomes non-zero (counts failing assertions) when an injected check fails'
$bc = Measure-BehaviouralGap -TestScript $crashS
Assert ($bc.gap -ge 1 -and $bc.exit_code -ne 0) 'a crashing child run is never reported as gap 0'
$rep = Get-LoopGapReport -RepoRoot (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path -TestScript $failS -IncludeR1
Assert ($rep.headline_gap -eq 2 -and $rep.presence_only.label -eq 'presence only' -and $rep.presence_only.total -eq 8) 'report headline is behavioural; keyword figure is secondary, labelled "presence only"'
# QA findings (run 43650162072, item 3): a run that executed no assertion, or whose FAIL line is wrapped in colour codes, is not gap 0.
$emptyS = Join-Path $inj 'empty.ps1'; $ansiS = Join-Path $inj 'ansi.ps1'; $earlyS = Join-Path $inj 'early.ps1'
Set-Content -LiteralPath $emptyS -Value ''
Set-Content -LiteralPath $ansiS -Value 'Write-Host "  ok: a"; Write-Host ([char]27 + "[31m  FAIL: coloured"); exit 0'
Set-Content -LiteralPath $earlyS -Value 'exit 0'
$be = Measure-BehaviouralGap -TestScript $emptyS
Assert ($be.gap -ge 1 -and $be.reason -eq 'no_assertions_ran') 'exit 0 with no assertion executed is never gap 0'
Assert ((Measure-BehaviouralGap -TestScript $earlyS).gap -ge 1) 'an early exit 0 is never gap 0'
$ba = Measure-BehaviouralGap -TestScript $ansiS
Assert ($ba.gap -eq 1 -and $ba.failing_assertions -eq 1) 'a FAIL line wrapped in ANSI colour codes is still counted'
Assert ($bp.passing_assertions -eq 1) 'passing assertions are counted too'
Remove-Item -LiteralPath $inj -Recurse -Force

Write-Host 'regression: resume, uncertain, simulation, stop, evidence (QA item 4)'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'r1'; $null = Leaf $t 'r2'
$null = Invoke-AgentDispatch $t 'r1' -Live -Sender $badSender; $null = Invoke-AgentDispatch $t 'r2' -Live -Sender $okSender
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("agent-tree-resume-" + [guid]::NewGuid().ToString('N') + '.clixml')
$t | Export-Clixml $tmp; $t2 = Import-Clixml $tmp; Remove-Item $tmp
Assert ((Invoke-AgentDispatch $t2 'r1' -Live -Sender $okSender).reason -eq 'uncertain_dispatch_no_replay' -and $t2.executions -eq 2) 'resume from saved state: an uncertain dispatch is still never replayed'
$null = Leaf $t2 'r3' 'r2'
Assert ((Invoke-AgentDispatch $t2 'r3' -Live -Sender $okSender).reason -eq 'concurrency_limit') 'resume from saved state: both slots are still held'
Assert ((Submit-ChildReport -Tree $t2 -NodeId 'r2' -Result success -Evidence 'e').reason -eq 'uncorrelated_report') 'an uncorrelated live node cannot be reported at all'
$null = Set-TreeStop $t2
Assert ((Invoke-AgentDispatch $t2 'r3' -Live -Sender $okSender).reason -eq 'stop_switch') 'stop switch also blocks live dispatch'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'v1'; $null = Invoke-AgentDispatch $t 'v1' -Live -Sender $okSender
$null = Confirm-AgentRun -Tree $t -NodeId 'v1' -RunUrl 'https://example.invalid/r' -ResponseUrl 'https://example.invalid/c'
$null = Submit-ChildReport -Tree $t -NodeId 'v1' -Result success
$t.nodes['v1'].evidence = @()
Assert ((Set-ParentVerdict -Tree $t -NodeId 'v1' -Actor 'root' -Verdict accept).reason -eq 'insufficient_evidence') 'a correlated report without evidence cannot be accepted'

Write-Host 'persistence'
$tmp = New-TemporaryFile; Save-AgentTree $t $tmp.FullName
Assert ((Get-Content $tmp -Raw | ConvertFrom-Json).schema -eq 'agent-tree-r1/v1') 'tree serialises to JSON'; Remove-Item $tmp

Write-Host 'Codex round 1 findings (independent review of 6c453ccef): each one red before its fix'
# P1-1: an uncertain send may still be running; a simulated report must neither free its slot nor allow a replay
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'; $null = Invoke-AgentDispatch $t 'a' -Live -Sender $badSender
Assert ((Submit-ChildReport -Tree $t -NodeId 'a' -Result unknown -Simulated).reason -eq 'simulated_report_on_live_node' -and $t.nodes['a'].status -eq 'uncertain') 'simulated report on an UNCERTAIN node is refused (it may be running)'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'u1'; $null = Leaf $t 'u2'; $null = Leaf $t 'u3' 'u2'
$null = Invoke-AgentDispatch $t 'u1' -Live -Sender $badSender; $null = Invoke-AgentDispatch $t 'u2' -Live -Sender $okSender
$null = Submit-ChildReport -Tree $t -NodeId 'u1' -Result unknown -Simulated
Assert ((Invoke-AgentDispatch $t 'u3' -Live -Sender $okSender).reason -eq 'concurrency_limit') 'uncertain slot cannot be released by a simulated report (3 live children never in flight)'
# P1-2 / P1-3: limits can only be lowered; a caller cannot widen them or rewind the clock
$t = New-AgentTree -RootId 'root' -Limits @{ MaxExecutions = 4; MaxConcurrent = 4; MaxNodes = 8; MaxDepth = 9; MaxAttempts = 5; WallClockMinutes = 600 }
Assert ($t.limits.MaxExecutions -eq 3 -and $t.limits.MaxConcurrent -eq 2 -and $t.limits.MaxNodes -eq 7 -and $t.limits.MaxDepth -eq 3 -and $t.limits.MaxAttempts -eq 2 -and $t.limits.WallClockMinutes -eq 30) 'limits passed by a caller can lower, never widen, the #855 caps'
$old = [datetime]::UtcNow.AddMinutes(-60)
$t = New-AgentTree -RootId 'root' -Now $old
Assert ((Leaf $t 'a').reason -eq 'wall_clock_exceeded') 'a tree 60 minutes old refuses new nodes even when the caller passes an old -Now'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'; $t.started_at = [datetime]::UtcNow.AddMinutes(-60)
Assert ((Invoke-AgentDispatch $t 'a' -Live -Sender $okSender -Now $old).reason -eq 'wall_clock_exceeded') 'live dispatch after 30 minutes is refused whatever -Now is passed'
# P1-4: reparenting updates the whole subtree
$t = New-AgentTree -RootId 'root' -UnsafeAllowWidenLimitsForTests -Limits @{ MaxConcurrent = 10; MaxDepth = 4 }
$null = Leaf $t 'ro'; $null = Add-AgentNode -Tree $t -NodeId 'ro2' -ParentId 'root' -Scope s -Objective o -Permissions read -BaseSha x
$null = Add-AgentNode -Tree $t -NodeId 'c' -ParentId 'root' -Scope sc -Objective o -BaseSha x; $null = Add-AgentNode -Tree $t -NodeId 'd' -ParentId 'c' -Scope sd -Objective o -BaseSha x
$mv = Move-AgentNode -Tree $t -NodeId 'c' -NewParentId 'ro2' -Requester 'root'
Assert ($mv.ok -and $t.nodes['d'].depth -eq 4 -and $t.nodes['d'].permissions -notcontains 'write') 'reparent recomputes descendant depth and intersects descendant permissions'
Assert ((Add-AgentNode -Tree $t -NodeId 'e' -ParentId 'd' -Scope se -Objective o -BaseSha x).reason -eq 'depth_limit') 'a node under a moved subtree is checked against its real depth'
# P1-5: a retry starts clean: no stale identity, no acceptance without a new execution, simulated retries are bounded
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'; $null = Invoke-AgentDispatch $t 'a' -Live -Sender $okSender
$null = Confirm-AgentRun -Tree $t -NodeId 'a' -RunUrl 'https://example.invalid/r1' -ResponseUrl 'https://example.invalid/c1'
$null = Submit-ChildReport -Tree $t -NodeId 'a' -Result failure -Evidence 'e'; $null = Set-ParentVerdict -Tree $t -NodeId 'a' -Actor 'root' -Verdict retry -NewApproach 'second'
Assert ((Confirm-AgentRun -Tree $t -NodeId 'a' -RunUrl 'https://example.invalid/r2' -ResponseUrl 'https://example.invalid/c2').reason -eq 'not_dispatched') 'after a retry the node cannot be correlated or accepted without a new live dispatch'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'; $n = 0
foreach ($i in 1..5) { $null = Submit-ChildReport -Tree $t -NodeId 'a' -Result unknown -Simulated; if ((Set-ParentVerdict -Tree $t -NodeId 'a' -Actor 'root' -Verdict retry -NewApproach "x$i").ok) { $n++ } }
Assert ($n -le 1) 'simulated report/retry cycles are bounded by the attempt cap (at most 1 retry of 2 attempts)'
# P1-6: inbound requests: closed kinds, scalar nonce
$t = New-AgentTree -RootId 'root' -AllowedSenders @('owner'); $null = Leaf $t 'a'
Assert ((Test-InboundRequest $t @{ sender = 'owner'; node_id = 'a'; nonce = 'k'; kind = 'unknown-kind' }).reason -eq 'malformed') 'unknown inbound kind is rejected'
Assert ((Test-InboundRequest $t @{ sender = 'owner'; node_id = 'a'; nonce = @('n', 'x'); kind = 'report' }).reason -eq 'malformed') 'a non-scalar nonce is rejected'
# P1-7: stderr noise on exit 0 is a gap, an ok: line cannot hide it
$inj2 = Join-Path ([IO.Path]::GetTempPath()) ("agent-tree-gap2-" + [guid]::NewGuid().ToString('N')); $null = New-Item -ItemType Directory -Path $inj2
$errS = Join-Path $inj2 'err.ps1'; Set-Content -LiteralPath $errS -Value 'Write-Host "  ok: setup"; Write-Error "ASSERTION FAILED" -ErrorAction Continue; exit 0'
Assert ((Measure-BehaviouralGap -TestScript $errS).gap -ge 1) 'error output on stderr with exit 0 is never gap 0'
Remove-Item -LiteralPath $inj2 -Recurse -Force
# stop switch halts every mutation; QA identity
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'; $null = Invoke-AgentDispatch $t 'a' -Live -Sender $okSender; Set-TreeStop $t
Assert ((Confirm-AgentRun -Tree $t -NodeId 'a' -RunUrl 'https://example.invalid/r' -ResponseUrl 'https://example.invalid/c').reason -eq 'stop_switch') 'stop switch blocks run correlation'
Assert ((Submit-ChildReport -Tree $t -NodeId 'a' -Result success -Simulated).reason -eq 'stop_switch') 'stop switch blocks reports'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'; $null = Invoke-AgentDispatch $t 'a' -Live -Agent 'claude' -Sender $okSender
Assert (-not (Request-IndependentQA -Tree $t -NodeId 'a' -QaIdentity 'claude' -Starter { @{ verdict = 'pass' } }).ok) 'QA by the producer agent name is refused (stays pending)'

if ($failures.Count) { Write-Host "`n$($failures.Count) FAILED" -ForegroundColor Red; exit 1 }
Write-Host "`nAll agent-tree-r1 tests passed (simulated dispatch only)." -ForegroundColor Green
