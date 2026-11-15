# GitHubSender.test.ps1 - deterministic; `gh` is a recording fake, no network, no real comment is posted.
#   pwsh Scripts/agent-tree-r1/GitHubSender.test.ps1
[CmdletBinding()] param()
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AgentTree.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GitHubSender.psm1') -Force
$failures = @()
function Assert { param([bool]$C, [string]$M)
    if ($C) { Write-Host "  ok: $M" -ForegroundColor Green } else { $script:failures += $M; Write-Host "  FAIL: $M" -ForegroundColor Red } }
function Throws { param([scriptblock]$B) try { & $B | Out-Null; return $false } catch { return $true } }
function Leaf { param($T, $Id, $Mode = 'read', $Sha = 'abc1234')
    Add-AgentNode -Tree $T -NodeId $Id -ParentId 'root' -Scope "scope-$Id" -Objective "obj $Id" -Mode $Mode -Baseline 4 -Target 0 -BaseSha $Sha }
$script:calls = @()
$goodGh = { param($a) $script:calls += , $a; 'https://github.com/GuitarAlchemist/ga/pull/858#issuecomment-1234567' }

Write-Host 'dry-run stays the default; the sender is never called'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'
$s = New-GitHubCommentSender -Repo 'GuitarAlchemist/ga' -Number 858 -Gh $goodGh
$r = Invoke-AgentDispatch -Tree $t -NodeId 'a' -Sender $s
Assert ($r.ok -and -not $r.launched -and $script:calls.Count -eq 0) 'without -Live nothing is posted'

Write-Host 'live post carries the receipt and the request identifier'
$r = Invoke-AgentDispatch -Tree $t -NodeId 'a' -Live -Sender $s
Assert ($r.launched -and $r.receipt -like '*#issuecomment-1234567' -and $t.nodes['a'].status -eq 'dispatched') 'receipt is the comment URL, node dispatched'
Assert ($script:calls.Count -eq 1 -and $script:calls[0][0] -eq 'issue' -and $script:calls[0] -contains '--repo') 'exactly one gh call'
$body = $script:calls[0][-1]
Assert ($body -match "idempotency_key: $($r.key)" -and $body -match 'base_sha: abc1234' -and $body -match 'READ-ONLY') 'comment quotes the key, the exact SHA and the mode'
Assert ($body -match 'do not create other child tasks') 'comment forbids further agents'

Write-Host 'no double post, no retry'
$again = $null; try { $again = & $s $t.nodes['a'] 'k2' } catch { $again = $_.Exception.Message }
Assert ($again -like '*never posts twice*' -and $script:calls.Count -eq 1) 'the same sender refuses a second post'

Write-Host 'doubt means uncertain, never replayed'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'
$noUrl = New-GitHubCommentSender -Repo 'GuitarAlchemist/ga' -Number 858 -Gh { param($a) 'done' }
$r = Invoke-AgentDispatch -Tree $t -NodeId 'a' -Live -Sender $noUrl
Assert (-not $r.ok -and $r.reason -eq 'uncertain' -and $t.nodes['a'].status -eq 'uncertain') 'an answer with no comment URL is uncertain'
Assert ((Invoke-AgentDispatch -Tree $t -NodeId 'a' -Live -Sender $noUrl).reason -eq 'uncertain_dispatch_no_replay') 'an uncertain node is not replayed'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'
$other = New-GitHubCommentSender -Repo 'GuitarAlchemist/ga' -Number 858 -Gh { param($a) 'https://github.com/GuitarAlchemist/ga/issues/1#issuecomment-9' }
Assert ((Invoke-AgentDispatch -Tree $t -NodeId 'a' -Live -Sender $other).reason -eq 'uncertain') 'a receipt for another target is uncertain'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'
$boom = New-GitHubCommentSender -Repo 'GuitarAlchemist/ga' -Number 858 -Gh { param($a) throw 'network down' }
Assert ((Invoke-AgentDispatch -Tree $t -NodeId 'a' -Live -Sender $boom).reason -eq 'uncertain') 'a gh failure is uncertain'

Write-Host 'guards of the tree still apply'
$t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a'; Set-TreeStop $t
$script:calls = @()
$s2 = New-GitHubCommentSender -Repo 'GuitarAlchemist/ga' -Number 858 -Gh $goodGh
Assert ((Invoke-AgentDispatch -Tree $t -NodeId 'a' -Live -Sender $s2).reason -eq 'stop_switch' -and $script:calls.Count -eq 0) 'the stop switch posts nothing'
Assert (Throws { New-GitHubCommentSender -Repo 'someone/else' -Number 1 -Gh $goodGh }) 'a repo outside the allow-list is refused'
Assert (Throws { New-GitHubCommentSender -Repo 'GuitarAlchemist/ga' -Number 0 -Gh $goodGh }) 'a missing target number is refused'
$t = New-AgentTree -RootId 'root'; $nb = Add-AgentNode -Tree $t -NodeId 'n' -ParentId 'root' -Scope 's' -Objective 'o' -Mode 'read' -Baseline 1 -Target 0
Assert ((Invoke-AgentDispatch -Tree $t -NodeId 'n' -Live -Sender (New-GitHubCommentSender -Repo 'GuitarAlchemist/ga' -Number 858 -Gh $goodGh)).reason -eq 'uncertain') 'a node with no base_sha cannot be dispatched'

Write-Host 'base_sha must be 7 to 40 hexadecimal characters'
foreach ($bad in 'not-a-sha', 'abc123', 'ghijklm', ('a' * 41), ' abc1234', '0xabc1234') {
    $script:calls = @(); $t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a' 'read' $bad
    $r = Invoke-AgentDispatch -Tree $t -NodeId 'a' -Live -Sender (New-GitHubCommentSender -Repo 'GuitarAlchemist/ga' -Number 858 -Gh $goodGh)
    Assert ($r.reason -eq 'uncertain' -and $t.nodes['a'].status -eq 'uncertain' -and $script:calls.Count -eq 0) "base_sha '$bad' is refused and nothing is posted"
}
foreach ($good in 'abc1234', ('A' * 7), ('0123456789abcdef' * 2 + '01234567')) {
    $script:calls = @(); $t = New-AgentTree -RootId 'root'; $null = Leaf $t 'a' 'read' $good
    $r = Invoke-AgentDispatch -Tree $t -NodeId 'a' -Live -Sender (New-GitHubCommentSender -Repo 'GuitarAlchemist/ga' -Number 858 -Gh $goodGh)
    Assert ($r.launched -and $t.nodes['a'].status -eq 'dispatched' -and $script:calls.Count -eq 1) "base_sha of $($good.Length) hex characters is accepted"
}

if ($failures.Count) { Write-Host "`n$($failures.Count) FAILED" -ForegroundColor Red; exit 1 }
Write-Host "`nAll GitHubSender tests passed (fake gh, nothing posted)." -ForegroundColor Green
