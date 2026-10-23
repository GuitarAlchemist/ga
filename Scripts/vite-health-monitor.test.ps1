param([string]$ScriptPath = (Join-Path $PSScriptRoot 'vite-health-monitor.ps1'))
$ErrorActionPreference = 'Stop'
$tokens = $null; $parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($ScriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors) { throw ($parseErrors | Out-String) }
foreach ($definition in $ast.FindAll({param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst]}, $true)) {
  . ([scriptblock]::Create($definition.Extent.Text))
}

# Exercise the real restart function with no network access or process launch.
function Write-Log { param($Level, $Message) }
function Emit-Algedonic { param($Severity, $Summary, $Details) }
function Push-Location { param($Path) }
function Pop-Location { }
function Start-Sleep { param($Seconds) }
function Get-NetTCPConnection {
  param($State, $ErrorAction)
  if ($script:listenerError) { throw 'listener inspection unavailable' }
  if ($script:occupiedPort) { [pscustomobject]@{LocalPort=$script:occupiedPort; OwningProcess=123} }
}
function Start-Process {
  param($FilePath, $ArgumentList, $WindowStyle, [switch]$PassThru, $ErrorAction)
  $script:starts.Add([pscustomobject]@{FilePath=$FilePath; ArgumentList=$ArgumentList; WindowStyle=$WindowStyle})
  $process = [pscustomobject]@{HasExited=$false}
  $process | Add-Member -MemberType ScriptMethod -Name Refresh -Value { }
  $process
}
function Assert-Equal($Expected, $Actual, $Message) {
  if ($Expected -ne $Actual) { throw "$Message : expected $Expected, got $Actual" }
  "PASS $Message"
}
function Reset-Fixture {
  $script:starts = [System.Collections.Generic.List[object]]::new()
  $script:occupiedPort = 0
  $script:listenerError = $false
  $script:pendingViteProcess = $null
}
$Port=5176; $WorkingDir='unused-fixture'; $StartupWaitSeconds=0
Reset-Fixture
$script:occupiedPort = 5176
Restart-Vite
Assert-Equal 0 $script:starts.Count 'two failed probes never spawn over an existing listener'

Reset-Fixture
$script:listenerError = $true
Restart-Vite
Assert-Equal 0 $script:starts.Count 'unknown listener state fails closed'

Reset-Fixture
$script:occupiedPort = 5177
Restart-Vite
Assert-Equal 1 $script:starts.Count 'a free target port allows one restart'
Assert-Equal 'Hidden' $script:starts[0].WindowStyle 'restart remains hidden'
Assert-Equal $true (($script:starts[0].ArgumentList -join ' ') -match '--port 5176 --strictPort') 'restart cannot drift onto a second port'
Restart-Vite
Assert-Equal 1 $script:starts.Count 'a still-running launch cannot be duplicated before it binds'
$script:pendingViteProcess.HasExited=$true
Restart-Vite
Assert-Equal 2 $script:starts.Count 'an exited launch may be retried if the target is free'

# A separate runspace supplies a different thread: mutex ownership is reentrant
# on the same thread and would otherwise give false confidence.
$lockPort=65176
$held = Get-ViteMonitorMutex -Port $lockPort
if ($null -eq $held) { throw 'test mutex unexpectedly busy' }
$runner = [powershell]::Create()
try {
  $null=$runner.AddScript({
    param($Source, $TargetPort)
    $tokens=$null; $errors=$null
    $ast=[System.Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
    $definition=$ast.Find({param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Get-ViteMonitorMutex'},$true)
    . ([scriptblock]::Create($definition.Extent.Text))
    $other=Get-ViteMonitorMutex -Port $TargetPort
    if ($null -eq $other) { 'blocked' } else { $other.ReleaseMutex(); $other.Dispose(); 'acquired' }
  }).AddArgument($ScriptPath).AddArgument($lockPort)
  $out=$runner.Invoke()
  if ($runner.HadErrors) { throw ($runner.Streams.Error | Out-String) }
  Assert-Equal 'blocked' ([string]$out[0]) 'a second monitor cannot acquire the same port lock'
} finally { $runner.Dispose(); $held.ReleaseMutex(); $held.Dispose() }
$again=Get-ViteMonitorMutex -Port $lockPort
if ($null -eq $again) { throw 'mutex was not released' }
$again.ReleaseMutex(); $again.Dispose()
'PASS monitor lock is released after exit'
