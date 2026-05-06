---
title: "Playwright tests against ghost UI — placeholder BaseUrl + ContentRoot mismatch + selector drift"
date: 2026-05-06
problem_type: "integration-issues"
component: "Tests/GuitarAlchemistChatbot.Tests.Playwright + Apps/ga-server/GaApi"
symptoms:
  - "All 198 Playwright tests fail with `net::ERR_CONNECTION_REFUSED at https://localhost:7001/` against a CI run where GaApi is fully up and serving on port 5232"
  - "ChatbotTestBase.cs has `BaseUrl = \"https://localhost:7001\"; // Update with actual URL` — port 7001 doesn't match any GaApi binding (5232 http, 7184 https, 41167 IIS)"
  - "GaApi captured logs show `WebRootPath was not found: D:\\a\\ga\\ga\\wwwroot. Static files may be unavailable.` — GaApi looks for wwwroot at workspace root, not at Apps/ga-server/GaApi/wwwroot where chatbot-demo.html actually lives"
  - "After URL+ContentRoot fix: failure mode shifts from ERR_CONNECTION_REFUSED to selector-not-found — tests use Bootstrap-style locators (`input.form-control[placeholder*='Ask about']`, `button.btn-primary:has(i.fa-paper-plane)`) but chatbot-demo.html uses different classes (`chat-input`, `id=\"messageInput\"`, custom `.send-btn`)"
tags:
  - "github-actions"
  - "playwright"
  - "aspnet-core"
  - "static-files"
  - "content-root"
  - "test-fixture-drift"
  - "placeholder-url"
related_patterns:
  - "placeholder-never-updated"
  - "content-root-vs-working-directory"
  - "test-against-imagined-ui"
severity: "high"
related_docs:
  - "docs/solutions/integration-issues/ga-ci-test-job-preconditions-artifacts-services-liveness-2026-05-05.md"
related_prs:
  - "GuitarAlchemist/ga#140"
related_issues:
  - "GuitarAlchemist/ga#134"
  - "GuitarAlchemist/ga#145"
---

# Playwright tests against ghost UI — placeholder BaseUrl + ContentRoot mismatch + selector drift

## Problem

The `GuitarAlchemistChatbot.Tests.Playwright` suite (198 UI tests) had been failing in CI for an unknown duration with `ERR_CONNECTION_REFUSED at https://localhost:7001/`. After fixing the upstream CI infra (issues ga#128, ga#134), the failure mode unmasked **three nested layers of test-fixture drift** — each making the suite test something it wasn't actually testing.

The pattern: the tests *looked* legitimate (real selectors, real assertions, plausible setup), but the code path between "Playwright launches" and "real chatbot UI loads in a browser" was broken at three independent points. Each point was silent on its own — only the cascade produced ERR_CONNECTION_REFUSED, which historically gets blamed on "GaApi isn't running" instead of "the URL was always wrong."

## Solution

### 1. BaseUrl was a placeholder that never matched any binding

**Symptom:** Every test hits `https://localhost:7001/`, which has nothing listening. GaApi binds 5232 (http), 7184 (https), 41167 (IIS Express) — never 7001.

**Root cause:** `Tests/GuitarAlchemistChatbot.Tests.Playwright/ChatbotTestBase.cs:11` literally says:

```csharp
protected const string BaseUrl = "https://localhost:7001"; // Update with actual URL
```

The `// Update with actual URL` comment is the smoking gun — this was a stub from the test scaffolding that nobody ever filled in. Since `BaseUrl` was a `const`, no env var or config could redirect it. Tests have **never** loaded a real page in CI.

**Fix:** make `BaseUrl` env-overridable with a working default:

```csharp
protected static readonly string BaseUrl =
    Environment.GetEnvironmentVariable("CHATBOT_BASE_URL")
    ?? "http://localhost:5232/chatbot-demo.html";
```

CI sets `CHATBOT_BASE_URL` explicitly to whatever the workflow actually serves, eliminating the magic-port dependency.

### 2. ASPNETCORE_CONTENTROOT defaulted to workspace root, not project root

**Symptom:** Even after fixing BaseUrl, tests would still fail because GaApi serves nothing on `/chatbot-demo.html`. Captured logs showed:

```
warn: Microsoft.AspNetCore.StaticFiles.StaticFileMiddleware[16]
      The WebRootPath was not found: D:\a\ga\ga\wwwroot. Static files may be unavailable.
```

**Root cause:** ASP.NET Core resolves `wwwroot` relative to **ContentRoot**, which defaults to **the current working directory**. In CI we launch GaApi via `Start-Process dotnet GaApi.dll` from the workspace root (`D:\a\ga\ga\`), so ContentRoot becomes the workspace root and `wwwroot` resolves to `D:\a\ga\ga\wwwroot` — which doesn't exist. The actual `wwwroot/chatbot-demo.html` lives at `Apps/ga-server/GaApi/wwwroot/`.

The static-file middleware silently skips serving when its WebRootPath is missing — *warning*, not *error*. Every static request returns 404, which a probe-based liveness check has no way to detect.

**Fix:** pin ContentRoot via env var before spawning GaApi:

```pwsh
$env:ASPNETCORE_CONTENTROOT = (Resolve-Path "Apps/ga-server/GaApi").Path
Start-Process -NoNewWindow -PassThru -FilePath "dotnet" -ArgumentList $apiDll ...
```

Add a **static-file sanity probe** in CI between the liveness probe and `dotnet test`:

```pwsh
try {
  $page = Invoke-WebRequest -Uri "http://localhost:5232/chatbot-demo.html" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
  Write-Host "chatbot-demo.html OK ($($page.StatusCode), $($page.Content.Length) bytes)"
} catch {
  Write-Host "::warning::chatbot-demo.html returned ... ContentRoot may still be wrong"
}
```

If ContentRoot is wrong, the warning fires upstream of Playwright — instead of letting all tests fail with selector-not-found and blaming the wrong layer.

### 3. Selectors test a UI that doesn't exist (the keystone discovery)

**Symptom:** With URL + ContentRoot fixed, tests still fail — now with `selector not found` / `TimeoutError` on `.chat-container`, `input.form-control[placeholder*='Ask about']`, `button.btn-primary:has(i.fa-paper-plane)`.

**Root cause:** `ChatbotTestBase.cs` was written against a **Bootstrap + FontAwesome** UI design that doesn't match `chatbot-demo.html`'s actual classes:

| Test expects | Actual chatbot-demo.html |
|---|---|
| `input.form-control[placeholder*='Ask about']` | `<input class="chat-input" id="messageInput" placeholder="Ask me about chords...">` |
| `button.btn-primary:has(i.fa-paper-plane)` | custom `.send-btn` (no Bootstrap, no FontAwesome) |
| `.assistant-message .message-text` | different structure |
| `.typing-indicator`, `.vex-tabdiv` | absent on this page |

The tests were written against an **imagined chatbot UI** — possibly a planned design that was never built, or a deleted/renamed page. They never validated against the real served page. The suite has been testing a ghost.

**Fix:** **out of scope for the CI infra layer** — needs a product decision (resolution paths in ga#145):
- A. Update tests to match the real UI
- B. Build a Bootstrap UI to match the tests' design intent
- C. Find the original UI in git history if it was deleted

This third layer is the keystone — fixing the first two only changes the failure message, not the count.

## Prevention

### 1. `// Update with actual URL` comments are bug reports

**Tied to:** Layer 1 (BaseUrl placeholder)

A `const` URL/path/secret with a "// Update" / "// TODO" / "// FIXME" comment is a *latent bug already filed*. Audit gates: pre-commit hook or CI lint that fails on `// Update.*URL` / `// FIXME.*localhost` / similar markers in test config. The cost of stopping at commit time is seconds; the cost of finding it via "all 198 tests have always been wrong" is months.

### 2. Static-file probes belong in CI between liveness and tests

**Tied to:** Layer 2 (ContentRoot mismatch silently 404s)

When tests load static files, the workflow must HTTP-GET the actual file the tests load and assert 200. ASP.NET Core's static-file middleware *warns* on missing WebRootPath rather than erroring — silent 404s become "all tests fail at selector lookup downstream." A 5-second probe upstream gives an actionable signal: `::warning::chatbot-demo.html returned ... ContentRoot may still be wrong`. Generalize: any test target that loads static assets needs an upstream "the asset itself loads" probe, not just a generic liveness check.

### 3. Test selectors must be validated against the real page at least once

**Tied to:** Layer 3 (selectors test imagined UI)

A new test fixture should — before any assertion — load the page and assert that *at least one of its selectors resolves*. CI can run this as a "selector-coverage" check separately from the assertions: `WaitForSelectorAsync(.chat-container, timeout=5s) → assert true`. If it fails, the suite is testing a ghost; fail loudly with the page content, don't let downstream tests keep timing out one by one. This is the integration-test analog of `--fail-on-zero-tests` — fail-on-zero-selectors.

## Related

### Sibling solutions
- [GA CI test-job preconditions — artifacts, services, liveness](ga-ci-test-job-preconditions-artifacts-services-liveness-2026-05-05.md) — Bugs #1-#4 of the same chain (artifact handoff, service start, liveness probe, step boundary)
- [OPTIC-K SAE Phase 1 partition + python_bin](optick-sae-phase1-partition-and-python-bin-2026-05-05.md) — same date sibling; another instance of "CI looked fine but a precondition was wrong"
- [Ollama client extraction hot-alloc](2026-03-10-ollama-client-extraction-hot-alloc-fix.md) — frontmatter schema reference

### Related PRs / issues
- [`ga#140`](https://github.com/GuitarAlchemist/ga/pull/140) — landed Layers 1 + 2 (URL fix, ContentRoot pin, sanity probe)
- [`ga#134`](https://github.com/GuitarAlchemist/ga/issues/134) — root issue (Playwright failure layer); Step 1 closed by #140, Step 2 → #145
- [`ga#145`](https://github.com/GuitarAlchemist/ga/issues/145) — Bug #6 / Layer 3 (selectors vs real UI); 3 resolution paths documented

### Compoundable principle

**Tests can fail in three orthogonal ways: at the network layer (URL/host), at the file layer (assets/routing), and at the assertion layer (selectors/values).** Fixing one without verifying the others just changes the error message. CI signal value increases when each layer has its own probe upstream of the layer below it: liveness → static-file → selector-coverage → assertions. Each layer's failure should produce its own diagnostic message, not let everything roll up to "all tests failed."
