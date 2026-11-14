# AgentTree.psm1 — GA-AGENT-TREE-R1 bounded recursive supervision (first slice).
# In-memory tree + JSON persistence. No network calls: live dispatch goes through an
# injected -Sender scriptblock, so default behaviour is dry-run and tests are deterministic.
Set-StrictMode -Version Latest

$script:Terminal = @('accepted', 'failed', 'blocked', 'escalated', 'cancelled', 'rejected')

function New-AgentTree {
    param(
        [Parameter(Mandatory)][string]$RootId,
        [string]$Objective = 'root',
        [hashtable]$Limits = @{},
        [string[]]$Permissions = @('read', 'write'),
        [string[]]$AllowedSenders = @(),
        [datetime]$Now = [datetime]::UtcNow
    )
    $lim = @{ MaxExecutions = 3; MaxNodes = 7; MaxDepth = 3; MaxAttempts = 2; MaxConcurrent = 2; WallClockMinutes = 30 }
    foreach ($k in $Limits.Keys) { $lim[$k] = $Limits[$k] }
    $tree = @{
        schema = 'agent-tree-r1/v1'; root_id = $RootId; stop = $false; limits = $lim
        started_at = $Now; executions = 0; nodes = [ordered]@{}; dispatches = @(); history = @()
        seen_nonces = @(); allowed_senders = $AllowedSenders
    }
    $tree.nodes[$RootId] = New-NodeRecord -Id $RootId -ParentId $null -Scope 'root' -Objective $Objective `
        -Perms $Permissions -Budget $lim.MaxExecutions -Mode 'read' -Depth 1
    $tree.nodes[$RootId].status = 'running'
    return $tree
}

function New-NodeRecord {
    param($Id, $ParentId, $Scope, $Objective, $Perms, $Budget, $Mode, $Depth,
          $Baseline = $null, $Target = $null, $Acceptance = @(), $BaseSha = $null, $HeadSha = $null)
    return @{
        node_id = $Id; parent_id = $ParentId; scope = $Scope; objective = $Objective
        baseline = $Baseline; target = $Target; after = $null; acceptance = $Acceptance
        base_sha = $BaseSha; head_sha = $HeadSha; budget = $Budget; permissions = @($Perms)
        mode = $Mode; depth = $Depth; status = 'pending'; attempts = 0; evidence = @()
        execution_identity = $null; report = $null; parent_verdict = $null
        simulated = $false; correlated = $false; qa = 'pending'; ancestry = @(); events = @()
    }
}

function Add-TreeEvent { param($Tree, $Node, [string]$Text)
    $e = "{0:o} {1}" -f [datetime]::UtcNow, $Text
    $Node.events += $e; $Tree.history += "$($Node.node_id): $e" }

function Test-TreeHalted {
    param($Tree, [datetime]$Now = [datetime]::UtcNow)
    if ($Tree.stop) { return 'stop_switch' }
    if (($Now - $Tree.started_at).TotalMinutes -gt $Tree.limits.WallClockMinutes) { return 'wall_clock_exceeded' }
    return $null
}

function Set-TreeStop { param($Tree) $Tree.stop = $true }

function Get-Ancestry { param($Tree, [string]$Id)
    $chain = @(); $cur = $Tree.nodes[$Id]
    while ($cur) { $chain += $cur.node_id; $cur = if ($cur.parent_id) { $Tree.nodes[$cur.parent_id] } else { $null } }
    return $chain
}

function Get-Subtree { param($Tree, [string]$Id)
    $out = @($Id)
    foreach ($n in $Tree.nodes.Values) { if ($n.parent_id -eq $Id) { $out += Get-Subtree $Tree $n.node_id } }
    return $out
}

function Add-AgentNode {
    param(
        [Parameter(Mandatory)]$Tree, [Parameter(Mandatory)][string]$NodeId, [Parameter(Mandatory)][string]$ParentId,
        [Parameter(Mandatory)][string]$Scope, [Parameter(Mandatory)][string]$Objective,
        [ValidateSet('read', 'write')][string]$Mode = 'read', [string[]]$Permissions = @('read', 'write'),
        $Baseline = $null, $Target = $null, [string[]]$Acceptance = @(), [string]$BaseSha, [string]$HeadSha,
        [int]$Budget = 1, [datetime]$Now = [datetime]::UtcNow
    )
    $h = Test-TreeHalted $Tree $Now; if ($h) { return @{ ok = $false; reason = $h } }
    if (-not $Tree.nodes.Contains($ParentId)) { return @{ ok = $false; reason = 'unknown_parent' } }
    if ($Tree.nodes.Contains($NodeId)) { return @{ ok = $false; reason = 'duplicate_node_id' } }
    $p = $Tree.nodes[$ParentId]
    if ($p.status -in $script:Terminal) { return @{ ok = $false; reason = 'parent_terminal' } }
    if ($Tree.nodes.Count -ge $Tree.limits.MaxNodes) { return @{ ok = $false; reason = 'node_limit' } }
    if ($p.depth + 1 -gt $Tree.limits.MaxDepth) { return @{ ok = $false; reason = 'depth_limit' } }
    $active = @($Tree.nodes.Values | Where-Object { $_.parent_id -eq $ParentId -and $_.status -notin $script:Terminal }).Count
    if ($active -ge $Tree.limits.MaxConcurrent) { return @{ ok = $false; reason = 'concurrency_limit' } }
    $perms = @($Permissions | Where-Object { $_ -in $p.permissions })   # intersection, never widening
    if ($Mode -eq 'write') {
        if ('write' -notin $perms) { return @{ ok = $false; reason = 'write_not_permitted' } }
        $w = $Tree.nodes.Values | Where-Object { $_.mode -eq 'write' -and $_.scope -eq $Scope -and $_.status -notin $script:Terminal }
        if ($w) { return @{ ok = $false; reason = 'duplicate_writer' } }
    }
    $b = [math]::Min($Budget, $p.budget)
    $n = New-NodeRecord -Id $NodeId -ParentId $ParentId -Scope $Scope -Objective $Objective -Perms $perms -Budget $b `
        -Mode $Mode -Depth ($p.depth + 1) -Baseline $Baseline -Target $Target -Acceptance $Acceptance -BaseSha $BaseSha -HeadSha $HeadSha
    $n.ancestry = @(Get-Ancestry $Tree $ParentId)
    $Tree.nodes[$NodeId] = $n
    Add-TreeEvent $Tree $n "created under $ParentId (mode=$Mode budget=$b perms=$($perms -join ','))"
    return @{ ok = $true; node = $n }
}

# Reparenting is root-mediated, cycle-checked, depth-checked, and can only shrink permissions/budget.
function Move-AgentNode {
    param([Parameter(Mandatory)]$Tree, [Parameter(Mandatory)][string]$NodeId, [Parameter(Mandatory)][string]$NewParentId,
          [Parameter(Mandatory)][string]$Requester)
    if ($Requester -ne $Tree.root_id) { return @{ ok = $false; reason = 'root_mediation_required' } }
    $h = Test-TreeHalted $Tree; if ($h) { return @{ ok = $false; reason = $h } }
    if (-not $Tree.nodes.Contains($NodeId) -or -not $Tree.nodes.Contains($NewParentId)) { return @{ ok = $false; reason = 'unknown_node' } }
    if ($NodeId -eq $Tree.root_id) { return @{ ok = $false; reason = 'root_immutable' } }
    if ($NewParentId -in (Get-Subtree $Tree $NodeId)) { return @{ ok = $false; reason = 'cycle' } }
    $n = $Tree.nodes[$NodeId]; $np = $Tree.nodes[$NewParentId]
    $maxSub = (Get-Subtree $Tree $NodeId | ForEach-Object { $Tree.nodes[$_].depth - $n.depth } | Measure-Object -Maximum).Maximum
    if ($np.depth + 1 + $maxSub -gt $Tree.limits.MaxDepth) { return @{ ok = $false; reason = 'depth_limit' } }
    $old = $n.parent_id
    $n.parent_id = $NewParentId; $n.depth = $np.depth + 1
    $n.permissions = @($n.permissions | Where-Object { $_ -in $np.permissions })
    $n.budget = [math]::Min($n.budget, $np.budget)
    $n.ancestry = @(Get-Ancestry $Tree $NewParentId) + @("(was child of $old)")
    Add-TreeEvent $Tree $n "reparented $old -> $NewParentId by $Requester"
    return @{ ok = $true; node = $n }
}

function Get-IdempotencyKey { param($Node, [int]$Attempt)
    $s = "$($Node.node_id)|$($Node.scope)|$($Node.base_sha)|$Attempt"
    $sha = [System.Security.Cryptography.SHA256]::Create()
    return (($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($s)) | ForEach-Object { $_.ToString('x2') }) -join '').Substring(0, 24)
}

# Dry-run by default: drafts a mandate, never counts as a launch. -Live needs -Sender returning
# @{ comment_url = ... } (receipt). Any sender exception => 'uncertain', which is never replayed.
function Invoke-AgentDispatch {
    param([Parameter(Mandatory)]$Tree, [Parameter(Mandatory)][string]$NodeId, [string]$Agent = 'claude',
          [switch]$Live, [scriptblock]$Sender, [datetime]$Now = [datetime]::UtcNow)
    $n = $Tree.nodes[$NodeId]
    if (-not $n) { return @{ ok = $false; reason = 'unknown_node' } }
    $h = Test-TreeHalted $Tree $Now; if ($h) { return @{ ok = $false; reason = $h } }
    if ($n.status -eq 'uncertain') { return @{ ok = $false; reason = 'uncertain_dispatch_no_replay' } }
    if ($n.status -ne 'pending') { return @{ ok = $false; reason = "bad_state_$($n.status)" } }
    if ($n.attempts -ge $Tree.limits.MaxAttempts) { return @{ ok = $false; reason = 'attempt_limit' } }
    $key = Get-IdempotencyKey $n ($n.attempts + 1)
    if ($Tree.dispatches | Where-Object { $_.key -eq $key -and $_.kind -eq 'live' }) { return @{ ok = $false; reason = 'duplicate_key' } }
    if ($n.mode -eq 'write') {
        $w = $Tree.nodes.Values | Where-Object { $_.node_id -ne $NodeId -and $_.mode -eq 'write' -and $_.status -in 'dispatched', 'uncertain' }
        if ($w) { return @{ ok = $false; reason = 'duplicate_writer' } }
    }
    if (-not $Live) {
        $Tree.dispatches += @{ key = $key; node_id = $NodeId; kind = 'drafted'; launched = $false }
        Add-TreeEvent $Tree $n "DRAFT only (dry-run), key=$key; not a launch"
        return @{ ok = $true; launched = $false; kind = 'drafted'; key = $key }
    }
    if (-not $Sender) { return @{ ok = $false; reason = 'no_sender' } }
    if ($Tree.executions -ge $Tree.limits.MaxExecutions) { return @{ ok = $false; reason = 'execution_limit' } }
    $Tree.executions++; $n.attempts++            # counted before send: an uncertain send still spends budget
    $rec = @{ key = $key; node_id = $NodeId; kind = 'live'; agent = $Agent; launched = $false; receipt = $null }
    $Tree.dispatches += $rec
    try {
        $r = & $Sender $n $key
        if (-not $r -or -not $r.comment_url) { throw 'no receipt' }
        $rec.receipt = $r.comment_url; $rec.launched = $true; $n.status = 'dispatched'
        $n.execution_identity = @{ agent = $Agent; key = $key; receipt = $r.comment_url; run_url = $null }
        Add-TreeEvent $Tree $n "dispatched to $Agent, receipt=$($r.comment_url) (run not yet correlated)"
        return @{ ok = $true; launched = $true; kind = 'live'; key = $key; receipt = $r.comment_url }
    } catch {
        $n.status = 'uncertain'
        Add-TreeEvent $Tree $n "dispatch UNCERTAIN: $($_.Exception.Message); will not be replayed"
        return @{ ok = $false; reason = 'uncertain'; key = $key }
    }
}

# Correlate receipt -> agent run + response. Without both URLs the node stays uncorrelated.
function Confirm-AgentRun { param($Tree, [string]$NodeId, [string]$RunUrl, [string]$ResponseUrl)
    $n = $Tree.nodes[$NodeId]
    if (-not $n -or -not $n.execution_identity) { return @{ ok = $false; reason = 'not_dispatched' } }
    if (-not $RunUrl -or -not $ResponseUrl) { return @{ ok = $false; reason = 'missing_correlation' } }
    $n.execution_identity.run_url = $RunUrl; $n.execution_identity.response_url = $ResponseUrl
    $n.correlated = $true; $n.evidence += @($RunUrl, $ResponseUrl)
    Add-TreeEvent $Tree $n "run correlated: $RunUrl"
    return @{ ok = $true }
}

# Leaf self-report. NOT acceptance.
function Submit-ChildReport { param($Tree, [string]$NodeId, [ValidateSet('success', 'failure', 'blocked', 'unknown')][string]$Result,
                                   [string[]]$Evidence = @(), $After = $null, [switch]$Simulated)
    $n = $Tree.nodes[$NodeId]
    if (-not $n) { return @{ ok = $false; reason = 'unknown_node' } }
    if ($n.status -notin 'dispatched', 'uncertain', 'pending') { return @{ ok = $false; reason = "bad_state_$($n.status)" } }
    if (-not $Simulated -and -not $n.correlated) { return @{ ok = $false; reason = 'uncorrelated_report' } }
    if ($Simulated) { $n.simulated = $true }
    $n.report = @{ result = $Result; evidence = $Evidence; after = $After }
    $n.evidence += $Evidence; $n.status = 'reported'
    Add-TreeEvent $Tree $n "self-report: $Result (awaiting parent verdict)"
    return @{ ok = $true }
}

# Only the immediate parent may decide. Verdicts: accept | retry | subordinate | escalate | reject
function Set-ParentVerdict {
    param([Parameter(Mandatory)]$Tree, [Parameter(Mandatory)][string]$NodeId, [Parameter(Mandatory)][string]$Actor,
          [Parameter(Mandatory)][ValidateSet('accept', 'retry', 'escalate', 'reject')][string]$Verdict,
          [string]$Reason = '', [string]$NewApproach = '')
    $n = $Tree.nodes[$NodeId]
    if (-not $n) { return @{ ok = $false; reason = 'unknown_node' } }
    if ($Actor -ne $n.parent_id) { return @{ ok = $false; reason = 'not_immediate_parent' } }
    if ($n.status -ne 'reported') { return @{ ok = $false; reason = 'no_report_to_judge' } }
    switch ($Verdict) {
        'accept' {
            if ($n.report.result -ne 'success') { return @{ ok = $false; reason = 'cannot_accept_non_success' } }
            if (-not $n.simulated -and (-not $n.correlated -or $n.evidence.Count -eq 0)) { return @{ ok = $false; reason = 'insufficient_evidence' } }
            $n.status = 'accepted'; $n.after = $n.report.after
        }
        'retry' {
            if ($n.attempts -ge $Tree.limits.MaxAttempts) { return @{ ok = $false; reason = 'attempt_limit' } }
            if (-not $NewApproach) { return @{ ok = $false; reason = 'retry_must_differ' } }
            $n.status = 'pending'; $n.correlated = $false; $n.report = $null; $n.objective += " [retry: $NewApproach]"
        }
        'reject' { $n.status = if ($n.report.result -eq 'blocked') { 'blocked' } else { 'failed' } }
        'escalate' {
            # Escalation only records a proposal for the parent's supervisor; it grants nothing.
            $n.status = 'escalated'
            $n.escalation = @{ to = $n.parent_id; proposal = $Reason; grants_privileges = $false }
        }
    }
    $n.parent_verdict = @{ verdict = $Verdict; by = $Actor; reason = $Reason }
    Add-TreeEvent $Tree $n "parent verdict by ${Actor}: $Verdict ($Reason)"
    return @{ ok = $true; status = $n.status }
}

# Inbound requests (e.g. child callbacks) are data: allow-listed sender, well-formed, single-use nonce.
function Test-InboundRequest { param($Tree, [hashtable]$Request)
    foreach ($f in 'sender', 'node_id', 'nonce', 'kind') { if (-not $Request.ContainsKey($f) -or -not $Request[$f]) { return @{ ok = $false; reason = 'malformed' } } }
    if ($Request.sender -notin $Tree.allowed_senders) { return @{ ok = $false; reason = 'unauthorized' } }
    if (-not $Tree.nodes.Contains($Request.node_id)) { return @{ ok = $false; reason = 'malformed' } }
    if ($Request.nonce -in $Tree.seen_nonces) { return @{ ok = $false; reason = 'replayed' } }
    $Tree.seen_nonces += $Request.nonce
    return @{ ok = $true }
}

# QA must run in a different identity and must be read-only; otherwise it stays pending.
function Request-IndependentQA { param($Tree, [string]$NodeId, [string]$QaIdentity, [scriptblock]$Starter)
    $n = $Tree.nodes[$NodeId]
    $producers = @($Tree.nodes.Values | Where-Object { $_.execution_identity } | ForEach-Object { $_.execution_identity.agent + ':' + $_.execution_identity.key })
    if (-not $Starter -or -not $QaIdentity -or $QaIdentity -in $producers -or $QaIdentity -eq $Tree.root_id) {
        $n.qa = 'pending'; Add-TreeEvent $Tree $n 'QA pending: no independent read-only context started'
        return @{ ok = $false; qa = 'pending' }
    }
    try { $r = & $Starter $n; $n.qa = if ($r.verdict) { $r.verdict } else { 'pending' } }
    catch { $n.qa = 'pending' }
    return @{ ok = $n.qa -ne 'pending'; qa = $n.qa }
}

# Master aggregate: gap before/after across accepted nodes; unaccepted nodes contribute no improvement.
function Get-TreeGap { param($Tree)
    $before = 0; $after = 0; $accepted = 0
    foreach ($n in $Tree.nodes.Values) {
        if ($null -eq $n.baseline) { continue }
        $before += $n.baseline
        if ($n.status -eq 'accepted' -and $null -ne $n.after) { $after += $n.after; $accepted++ } else { $after += $n.baseline }
    }
    return @{ before = $before; after = $after; closed = $before - $after; accepted_nodes = $accepted }
}

function Save-AgentTree { param($Tree, [string]$Path) $Tree | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding utf8 }

# Measures the concrete R1 gap: capabilities the one-cycle loop lacks, by grep over loop files.
function Measure-LoopGap { param([string]$RepoRoot, [switch]$IncludeR1)
    $files = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'Scripts') -File -Include *.ps1, *.psm1 -Recurse |
        Where-Object { $IncludeR1 -or $_.FullName -notmatch 'agent-tree-r1' })
    $files += Get-Item -LiteralPath (Join-Path $RepoRoot '.claude/skills/supervised-loop/SKILL.md')
    $caps = [ordered]@{
        durable_parent_id_nodes = 'parent_id'
        parent_verdict_on_child = 'Set-ParentVerdict|parent_verdict'
        dispatch_idempotency_key = 'idempotency'
        cycle_rejection = "'cycle'|reject.*cycle"
        cumulative_descendant_budget = 'MaxExecutions|cumulative'
        dispatch_receipt_run_correlation = 'Confirm-AgentRun|run_url'
        independent_qa_pending = 'QA pending|qa_pending'
        uncertain_dispatch_no_replay = 'uncertain_dispatch_no_replay'
    }
    $missing = @(); $present = @()
    foreach ($c in $caps.Keys) {
        $hit = $files | Select-String -Pattern $caps[$c] -List -ErrorAction SilentlyContinue
        if ($hit) { $present += $c } else { $missing += $c }
    }
    return @{ total = $caps.Count; present = $present; missing = $missing; gap = $missing.Count }
}

Export-ModuleMember -Function *-*
