---
title: "GA CI test-job preconditions — missing artifacts, unstarted GaApi service, and 401-blind liveness probe"
date: 2026-05-05
problem_type: "integration-issues"
component: ".github/workflows/ci.yml"
symptoms:
  - "dotnet test --no-build fails with 'test source file ... was not found' for every *.Tests.dll because the build job's `**/bin/Release/**` artifact was uploaded but never downloaded by test-backend or test-playwright"
  - "Playwright suite aborts with `ECONNREFUSED ::1:5232` — no GaApi process is started in CI, so http://localhost:5232 has nothing listening"
  - "Liveness loop reports 'GaApi failed to start within 180s' even though gaapi-stdout.log shows repeated `Request finished HTTP/1.1 GET http://localhost:5232/api/chatbot/status - 401`; PowerShell `Invoke-WebRequest` throws on 4xx, the silent `catch { Start-Sleep }` retried forever, so a fully responsive API returning 401 (no auth header in CI) was never recognized as up"
tags:
  - "github-actions"
  - "dotnet-test"
  - "actions-artifacts"
  - "powershell"
  - "playwright"
  - "http-probe"
  - "liveness-check"
  - "background-process"
related_patterns:
  - "needs-artifact-handoff"
  - "background-service-readiness-gate"
  - "http-vs-network-error-distinction"
severity: "high"
related_docs:
  - "docs/solutions/integration-issues/optick-sae-phase1-partition-and-python-bin-2026-05-05.md"
  - "docs/solutions/integration-issues/2026-03-10-ag-ui-scale-event-sse-streaming-frontend-bridge.md"
  - "docs/solutions/integration-issues/2026-03-10-ollama-client-extraction-hot-alloc-fix.md"
related_prs:
  - "GuitarAlchemist/ga#122"
  - "GuitarAlchemist/ga#123"
  - "GuitarAlchemist/ga#105"
---

# GA CI test-job preconditions — missing artifacts, unstarted GaApi service, and 401-blind liveness probe

## Problem

GitHub Actions CI for the `ga` repo failed three runs in a row because three nested infrastructure bugs were hiding each other. Every PR that touched `.github/workflows/ci.yml` paths (and several that did not) showed Backend Tests + Playwright Tests both red, with no semantic relationship to the PR's actual content. The chain unmasked one bug at a time as each was fixed:

1. Test jobs ran `dotnet test --no-build` without the upstream `build` job's `**/bin/Release/**` artifact, so every test DLL was absent on the runner.
2. Once artifacts arrived, Playwright tests still failed because no `GaApi` process was running on `localhost:5232` for them to talk to.
3. Once `GaApi` was started in the background, the liveness probe still timed out — because the API was healthy and serving HTTP 401 in the CI auth context, and the probe accepted only HTTP 200 as "up."

## Solution

### 1. Missing build artifacts in test jobs

**Symptom:** `dotnet test --no-build` fails immediately for every `*.Tests.dll` with errors like `The test source file "...\bin\Release\net10.0\Foo.Tests.dll" was not found.` (×7+ projects), exit code 1.

**Root cause:** The upstream `build` job uploads `**/bin/Release/**` as the `build-artifacts` artifact so downstream jobs can run with `--no-build`. The `test-backend` and `test-playwright` jobs declared `needs: build` but never downloaded the artifact, so the runner had a fresh checkout with no compiled DLLs at the `Tests/.../bin/Release/net10.0/` paths the csproj layout expects.

**Fix:** Add an `actions/download-artifact@v4` step before any `dotnet test --no-build`. It restores files into the workspace at the same relative paths they were uploaded from.

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: ${{ env.DOTNET_VERSION }}

- name: Download build artifacts
  uses: actions/download-artifact@v4
  with:
    name: build-artifacts

- name: Restore dependencies
  run: dotnet restore AllProjects.slnx
```

### 2. API service never started for Playwright

**Symptom:** Every Playwright test fails with `ECONNREFUSED ::1:5232` — no service contact, all assertions fail.

**Root cause:** Playwright tests connect to `http://localhost:5232` (the `http` profile in `Apps/ga-server/GaApi/Properties/launchSettings.json`), but CI never started GaApi. `dotnet test` does not spawn the API; it only runs the test assembly. The runner had nothing listening on 5232.

**Fix:** After downloading artifacts, `Start-Process` GaApi in the background with stdout/stderr redirected to log files, pin the port via `ASPNETCORE_URLS`, then poll for liveness before running tests. Always upload the captured logs as an artifact for post-mortem.

```yaml
- name: Start GaApi (background)
  shell: pwsh
  run: |
    $apiDll = "Apps/ga-server/GaApi/bin/Release/net10.0/GaApi.dll"
    if (-not (Test-Path $apiDll)) {
      throw "GaApi.dll not found at $apiDll - build artifacts may not have downloaded correctly"
    }
    $env:ASPNETCORE_URLS = "http://localhost:5232"
    $env:ASPNETCORE_ENVIRONMENT = "CI"
    $start = Get-Date
    Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList $apiDll `
      -RedirectStandardOutput "gaapi-stdout.log" `
      -RedirectStandardError "gaapi-stderr.log"
    # ... liveness probe (see #3) ...

- name: Upload GaApi logs (always)
  uses: actions/upload-artifact@v4
  if: always()
  with:
    name: gaapi-logs
    path: |
      gaapi-stdout.log
      gaapi-stderr.log
    if-no-files-found: ignore
    retention-days: 7
```

**Probe-window note:** the deadline must cover real cold-start cost. GaApi loads voicing/embedding binary caches, Yarp config, and the governance watcher before serving requests. The first attempt used 60s and timed out at the cache-loading stage (~30s observed); the working window is **180s** with elapsed-time computed against `$start`, not the deadline.

### 3. Liveness probe accepts only 200 (the keystone learning)

**Symptom:** GaApi was fully up and serving HTTP on port 5232, but the probe loop ran the entire 180s window and the job failed with `::error::GaApi failed to start within 180s` — against a healthy server. Captured logs showed Kestrel listening and serving, but `/api/chatbot/status` returned **401 Unauthorized** in the CI environment (no auth context). The 200-only check treated every 401 as "still down" and silently retried.

**Root cause:** The probe conflated two orthogonal questions: *is the process listening?* (liveness) vs. *is this endpoint correctly authorized?* (auth correctness). Liveness is what blocks the test job; auth correctness is the test's job. In PowerShell, `Invoke-WebRequest` **throws** on any 4xx/5xx, so the original `try { if ($r.StatusCode -eq 200) {...} } catch { Start-Sleep }` pattern dumped 401/404/500 (all proof the server is up) into the same bucket as connection refused, timeout, and DNS failure (all proof the server is *not* up).

**Fix — distinguish HTTP errors from network errors via `$_.Exception.Response`.** When `Invoke-WebRequest` throws on a 4xx/5xx, `$_.Exception.Response` is **non-null** and exposes `.StatusCode`. When it throws on a network-level failure (ECONNREFUSED, timeout, DNS), `$_.Exception.Response` is **`$null`**. That single check cleanly separates "server alive but rejecting this request" from "nothing listening yet."

```powershell
$deadline = $start.AddSeconds(180)
# Treat ANY HTTP response (200, 401, 404, etc.) as "server up" — the goal
# is liveness, not auth correctness. Only true network errors (connection
# refused, timeout, DNS) keep polling.
while ((Get-Date) -lt $deadline) {
  try {
    $r = Invoke-WebRequest -Uri "http://localhost:5232/api/chatbot/status" `
           -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
    $elapsed = [math]::Round(((Get-Date) - $start).TotalSeconds, 1)
    Write-Host "GaApi up after ${elapsed}s (HTTP $($r.StatusCode))"
    exit 0
  } catch {
    $resp = $_.Exception.Response
    if ($resp -ne $null) {
      # HTTP error response (4xx/5xx) — server is alive and serving HTTP.
      $code = [int]$resp.StatusCode
      $elapsed = [math]::Round(((Get-Date) - $start).TotalSeconds, 1)
      Write-Host "GaApi up after ${elapsed}s (HTTP $code, treating non-2xx as live)"
      exit 0
    }
    # No Response object => network-level failure (refused/timeout/DNS).
    # Server isn't listening yet — keep polling.
    Start-Sleep -Seconds 3
  }
}
Write-Host "::error::GaApi failed to start within 180s"
Write-Host "--- gaapi-stdout.log (tail 80) ---"
if (Test-Path gaapi-stdout.log) { Get-Content gaapi-stdout.log -Tail 80 }
Write-Host "--- gaapi-stderr.log (tail 80) ---"
if (Test-Path gaapi-stderr.log) { Get-Content gaapi-stderr.log -Tail 80 }
exit 1
```

**Compoundable principle:** A liveness probe answers "is it listening?", not "is this endpoint healthy and authorized?" — those are downstream concerns for the tests themselves. In any HTTP-client language with exceptions on non-2xx (PowerShell `Invoke-WebRequest`, .NET `HttpClient.EnsureSuccessStatusCode`, Python `requests.raise_for_status`, Node `axios`), use the presence of a response object on the exception to reclassify HTTP errors as "alive." Reserve retry-and-wait for true network failures only. This pattern generalizes to any background-service-then-poll CI step (databases, message brokers, tunnels, sidecars).

## Prevention

### 1. CI jobs that consume build outputs must declare an explicit artifact dependency, and `--no-build` runs must fail loudly when the expected DLL is absent

**Tied to:** Bug #1 (`dotnet test --no-build` ran without the upstream `build` job's artifacts, every test DLL "not found")

Any job using `--no-build`, `--no-restore`, or otherwise assuming prior compilation must `needs:` the producing job AND `download-artifact` its output, then assert the expected binary exists (e.g. `test -f bin/.../Tests.dll`) before invoking the runner — so a missing artifact fails the setup step with a clear message instead of cascading into N opaque "test DLL not found" lines.

### 2. Any test that talks to a service over a port must start that service in the same workflow and gate on readiness before the test step runs

**Tied to:** Bug #2 (Playwright tests dialed `localhost:5232` with no GaApi started in CI)

For each `localhost:<port>` reference in a test config, the workflow must contain a step that launches the corresponding service (background process or service container) plus a readiness gate that polls the port until it responds — and the test step must `needs:` that gate. Grep CI configs for hardcoded `localhost:` ports and reject any whose owning service isn't started in the same job.

### 3. Liveness probes treat any HTTP response as "up"; only connection-level errors (refused, timeout, DNS) count as "down" and trigger retry

**Tied to:** Bug #3 (probe required HTTP 200, missed a healthy API returning 401/404 in CI auth context)

A liveness check is asking "is the socket answering?", not "am I authorized?" — so the probe should retry only on `ECONNREFUSED` / timeout / DNS failure, and treat 2xx/3xx/4xx/5xx all as proof the server is up. Reserve status-code assertions for separate functional checks downstream of liveness.

## Related

### Sibling solutions
- [OPTIC-K SAE Phase 1 partition drift + Windows python3 stub](optick-sae-phase1-partition-and-python-bin-2026-05-05.md) — closest precedent: same date, same `integration-issues` category, also a CI/cross-platform infra repair compounded via `/ce-compound`.
- [AG-UI scale-event SSE streaming frontend bridge](2026-03-10-ag-ui-scale-event-sse-streaming-frontend-bridge.md) — same producer/consumer wiring pattern at a different layer ("consumer expects a producer that isn't running").
- [Ollama client extraction hot-alloc fix](2026-03-10-ollama-client-extraction-hot-alloc-fix.md) — same `integration-issues` directory; mined for frontmatter schema (title/date/problem_type/component/symptoms/tags/related_patterns/severity/related_docs).

### Related PRs
- [`GuitarAlchemist/ga#122`](https://github.com/GuitarAlchemist/ga/pull/122) — initial CI infra repair (artifact download + GaApi background-start). Squash-merged 2026-05-05; the 401-blind probe portion was reverted before merge and is restored in the follow-up below.
- [`GuitarAlchemist/ga#123`](https://github.com/GuitarAlchemist/ga/pull/123) — format precedent: `/ce-compound` output for the SAE Phase 1 partition + python_bin learning, schema-validated YAML frontmatter, trim-tested prevention rules.
- [`GuitarAlchemist/ga#105`](https://github.com/GuitarAlchemist/ga/pull/105) — adjacent CI infra fix on the same workflow (Build Frontend dedupe). Part of the May 2026 CI infra cleanup cluster.

### External references
- [`actions/download-artifact` v4 docs](https://github.com/actions/download-artifact) — required pairing with `actions/upload-artifact@v4`; v3 artifacts are not interoperable.
- [PowerShell `Invoke-WebRequest` reference](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/invoke-webrequest) — documents the throw-on-non-2xx behavior; the `$_.Exception.Response.StatusCode` extraction is the idiom that makes the 401-blind probe work.
