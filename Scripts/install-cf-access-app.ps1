# install-cf-access-app.ps1
#
# Provisions the Cloudflare Access Application for the GA Development
# Dashboard via the Cloudflare REST API. Idempotent: re-running updates
# the existing app + policy rather than duplicating.
#
# Run:
#   $env:CF_API_TOKEN = "<token from https://dash.cloudflare.com/profile/api-tokens>"
#   pwsh -NoProfile -File Scripts\install-cf-access-app.ps1
#
# What it creates (matches docs/runbooks/cf-access-dashboard.md):
#   - Self-hosted Access app on demos.guitaralchemist.com, path /actions/*
#   - 24h session, name "GA Dev Dashboard — Actions"
#   - Single-email Allow policy: spareilleux@gmail.com
#   - One-time PIN as the identity provider (zero-config)
#
# Required API token scopes:
#   - Account / Access: Apps and Policies / Edit
#   - Zone / Zone / Read   (to look up the zone for guitaralchemist.com)

[CmdletBinding()]
param(
  [string]$ApiToken      = $env:CF_API_TOKEN,
  [string]$AccountId     = $env:CF_ACCOUNT_ID,
  [string]$AppDomain     = "demos.guitaralchemist.com",
  [string]$AppName       = "GA Dev Dashboard — Actions",
  [string]$AppPath       = "/actions/*",
  [string]$OperatorEmail = "spareilleux@gmail.com",
  [int]   $SessionHours  = 24
)

$ErrorActionPreference = "Stop"

if (-not $ApiToken) {
  Write-Host ""
  Write-Host "Missing CF_API_TOKEN." -ForegroundColor Red
  Write-Host "Create one at:" -ForegroundColor Yellow
  Write-Host "  https://dash.cloudflare.com/profile/api-tokens" -ForegroundColor Cyan
  Write-Host "Required scopes:" -ForegroundColor Yellow
  Write-Host "  - Account / Access: Apps and Policies / Edit"
  Write-Host "  - Zone / Zone / Read"
  Write-Host ""
  Write-Host "Then run:" -ForegroundColor Yellow
  Write-Host '  $env:CF_API_TOKEN = "<paste-token>"' -ForegroundColor Cyan
  Write-Host "  pwsh -NoProfile -File Scripts\install-cf-access-app.ps1" -ForegroundColor Cyan
  exit 2
}

$base    = "https://api.cloudflare.com/client/v4"
$headers = @{
  "Authorization" = "Bearer $ApiToken"
  "Content-Type"  = "application/json"
}

function Invoke-Cf {
  param([string]$Method, [string]$Path, $Body)
  $uri = "$base$Path"
  $args = @{ Method = $Method; Uri = $uri; Headers = $headers }
  if ($null -ne $Body) {
    $args.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
  }
  try {
    $r = Invoke-RestMethod @args
  } catch {
    $msg = $_.Exception.Message
    if ($_.ErrorDetails.Message) { $msg = $_.ErrorDetails.Message }
    Write-Host "API call failed: $Method $Path" -ForegroundColor Red
    Write-Host $msg -ForegroundColor Red
    exit 3
  }
  if (-not $r.success) {
    Write-Host "Cloudflare API returned success=false:" -ForegroundColor Red
    $r.errors | ConvertTo-Json -Depth 5 | Write-Host
    exit 3
  }
  return $r.result
}

# 1. Verify token + resolve account id if not provided
Write-Host "→ Verifying API token..." -ForegroundColor Gray
$verify = Invoke-Cf -Method "GET" -Path "/user/tokens/verify"
Write-Host "  Token id $($verify.id) status=$($verify.status)" -ForegroundColor Green

if (-not $AccountId) {
  Write-Host "→ Looking up account id..." -ForegroundColor Gray
  $accounts = Invoke-Cf -Method "GET" -Path "/accounts?per_page=50"
  if ($accounts.Count -eq 0) {
    throw "Token has no accounts visible — re-check scopes"
  }
  if ($accounts.Count -gt 1) {
    Write-Host "Multiple accounts visible. Pass -AccountId explicitly:" -ForegroundColor Yellow
    $accounts | ForEach-Object { Write-Host "  $($_.id)  $($_.name)" }
    exit 2
  }
  $AccountId = $accounts[0].id
  Write-Host "  Using account: $($accounts[0].name) ($AccountId)" -ForegroundColor Green
}

# 2. Look up zone id for guitaralchemist.com (sanity check; not strictly required for Account-scoped Access apps)
Write-Host "→ Looking up zone for guitaralchemist.com..." -ForegroundColor Gray
$zones = Invoke-Cf -Method "GET" -Path "/zones?name=guitaralchemist.com"
if ($zones.Count -eq 0) {
  Write-Host "  WARN: zone not found in this account; continuing with account-scoped Access app" -ForegroundColor Yellow
} else {
  Write-Host "  Zone id $($zones[0].id) status=$($zones[0].status)" -ForegroundColor Green
}

# 3. Check if app already exists (idempotency)
Write-Host "→ Checking for existing Access app on $AppDomain$AppPath..." -ForegroundColor Gray
$apps = Invoke-Cf -Method "GET" -Path "/accounts/$AccountId/access/apps?per_page=50"
$existing = $apps | Where-Object { $_.domain -eq "$AppDomain$AppPath" -or ($_.name -eq $AppName) }

$appBody = @{
  name              = $AppName
  domain            = "$AppDomain$AppPath"
  type              = "self_hosted"
  session_duration  = "${SessionHours}h"
  allowed_idps      = @()       # empty = One-time PIN (Cloudflare default)
  auto_redirect_to_identity = $false
  app_launcher_visible = $false
  http_only_cookie_attribute = $true
  same_site_cookie_attribute = "lax"
}

if ($existing) {
  Write-Host "  Found existing app id $($existing.id) — updating..." -ForegroundColor Yellow
  $app = Invoke-Cf -Method "PUT" -Path "/accounts/$AccountId/access/apps/$($existing.id)" -Body $appBody
} else {
  Write-Host "  Creating new app..." -ForegroundColor Gray
  $app = Invoke-Cf -Method "POST" -Path "/accounts/$AccountId/access/apps" -Body $appBody
}
Write-Host "  App id: $($app.id)" -ForegroundColor Green

# 4. Create or replace the Allow policy
Write-Host "→ Setting Allow policy for $OperatorEmail..." -ForegroundColor Gray
$policies = Invoke-Cf -Method "GET" -Path "/accounts/$AccountId/access/apps/$($app.id)/policies"
$policyBody = @{
  name      = "Operator only"
  decision  = "allow"
  include   = @(
    @{ email = @{ email = $OperatorEmail } }
  )
  require   = @()
  exclude   = @()
  precedence = 1
}
$existingPolicy = $policies | Where-Object { $_.name -eq "Operator only" } | Select-Object -First 1
if ($existingPolicy) {
  Write-Host "  Updating existing policy id $($existingPolicy.id)..." -ForegroundColor Yellow
  $policy = Invoke-Cf -Method "PUT" -Path "/accounts/$AccountId/access/apps/$($app.id)/policies/$($existingPolicy.id)" -Body $policyBody
} else {
  $policy = Invoke-Cf -Method "POST" -Path "/accounts/$AccountId/access/apps/$($app.id)/policies" -Body $policyBody
}
Write-Host "  Policy id: $($policy.id)" -ForegroundColor Green

# 5. Smoke test from this machine
Write-Host ""
Write-Host "✅ Cloudflare Access app + policy installed." -ForegroundColor Green
Write-Host ""
Write-Host "Edge propagation: <60s. Verifying now..." -ForegroundColor Gray
Start-Sleep -Seconds 5
$read = try { (Invoke-WebRequest -Uri "https://$AppDomain/dev-data/manifest" -Method Head -UseBasicParsing -ErrorAction Stop).StatusCode } catch { $_.Exception.Response.StatusCode.value__ }
$action = try { (Invoke-WebRequest -Uri "https://$AppDomain/actions/harness/skill/test-plan" -Method Post -Body '{}' -ContentType 'application/json' -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop).StatusCode } catch { $_.Exception.Response.StatusCode.value__ }

Write-Host "  GET /dev-data/manifest → $read (expected 200 — read stays public)"
if ("$read" -eq "200") { Write-Host "  ✓ read path OK" -ForegroundColor Green } else { Write-Host "  ⚠ unexpected" -ForegroundColor Yellow }
Write-Host "  POST /actions/harness/skill/test-plan → $action (expected 302 — CF Access redirect to login)"
if ("$action" -eq "302") { Write-Host "  ✓ gate active" -ForegroundColor Green } else { Write-Host "  ⚠ gate NOT active yet (may need a few more seconds to propagate)" -ForegroundColor Yellow }

Write-Host ""
Write-Host "Now open https://$AppDomain/test#dev/harness in a private window." -ForegroundColor Cyan
Write-Host "Click the 'Sign in' chip → CF emails a PIN → enter → chip should read 'Logged in as $OperatorEmail'." -ForegroundColor Cyan
