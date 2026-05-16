---
title: "GA CI Backend Tests — static-web-assets, Coverlet+ILGPU, perf thresholds, timer race"
date: 2026-05-14
problem_type: "integration-issues"
component: ".github/workflows/ci.yml + Tests/Apps/GaApi.Tests + Tests/Common/GA.Core.Tests + Tests/Common/GA.Business.Core.Tests + Common/GA.Business.Core.Analysis.Gpu"
symptoms:
  - "Backend Tests step on PR #210 reported 'Test Run Failed' with 4 distinct failure clusters that had been red across multiple commits"
  - "ChordNamingGraphQLTests.AllNames_Contains_At_Least_One_And_Includes_Best and BestName_Returns_NonEmpty_String threw DirectoryNotFoundException: D:\\a\\ga\\ga\\Apps\\ga-server\\GaApi\\obj\\Release\\net10.0\\compressed\\ before any test method ran — entire fixture failed at host startup"
  - "LazyWithExpirationTests.TimerStartsOnFirstAccess_NotOnConstruction asserted counter==2 but got counter==1 after 300ms sleep past a 100ms expiration window — slow CI runner Timer callback hadn't fired"
  - "GpuVoicingSearchPerformanceTests.SearchAsync_MultipleQueries_ShouldMaintainPerformance asserted avg search time < 50ms; CI shared runner hit 83ms"
  - "SetClassGpuAnalyzerTests.AnalyzeSpectra_ComputesCentroidCloseToCpu / AnalyzeSpectra_MatchesCpuMagnitudes / Provider_CachesCpuAnalyzerInstances threw ILGPU.InternalCompilerException → System.NotSupportedException: Cannot load from the static field 'Int32[] HitsArray' since it is not read only"
tags:
  - "github-actions"
  - "dotnet-test"
  - "webapplicationfactory"
  - "static-web-assets"
  - "coverlet"
  - "ilgpu"
  - "xplat-code-coverage"
  - "exclude-from-code-coverage"
  - "timer-race"
  - "perf-threshold"
  - "ci-shared-runner"
related_patterns:
  - "test-host-startup-fragility"
  - "coverage-instrumentation-vs-codegen"
  - "absolute-vs-relative-perf-assertions"
  - "fixed-sleep-vs-spin-wait"
severity: "high"
related_docs:
  - "docs/solutions/integration-issues/ga-ci-test-job-preconditions-artifacts-services-liveness-2026-05-05.md"
fix_commit: "be841339"
---

# Backend Tests was red on PR #210 across 4 unrelated clusters

The full Backend Tests step on the `feat/chatbot-showcase-demo-mode` branch had been
red for many commits — the failures looked test-related but on inspection were four
distinct CI-environment issues that all surface together because the .NET test runner
reports the union of failures.

## 1. WebApplicationFactory expects `obj/<Config>/net10.0/compressed/` to exist

### Symptom

```
System.IO.DirectoryNotFoundException : D:\a\ga\ga\Apps\ga-server\GaApi\obj\Release\net10.0\compressed\
   at Microsoft.Extensions.FileProviders.PhysicalFileProvider..ctor(String root, ExclusionFilters filters)
   at Microsoft.AspNetCore.Hosting.StaticWebAssets.StaticWebAssetsLoader.UseStaticWebAssetsCore(...)
```

The error fires from `PhysicalFileProvider`'s ctor BEFORE any test method runs. Every
test in the fixture reports the same exception, so it looks like 6 distinct test
failures when it's really one host-startup failure.

### Root cause

`Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory` auto-loads the
`<assembly>.staticwebassets.runtime.json` manifest. The manifest references
`obj/<Config>/net10.0/compressed/` as a ContentRoot for gzipped static assets.
Locally that directory exists (MSBuild's StaticWebAssetsCompressTask materializes
it when there's content to compress). On a clean CI Release build with no compressed
output, the manifest still names the path but the directory is missing —
`PhysicalFileProvider`'s ctor walks the path and throws.

### Fix

In the test factory, pre-create the empty directory for both build configurations
before the host boots:

```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    var contentRoot = TestPaths.RepositoryPath("Apps", "ga-server", "GaApi");
    builder.UseContentRoot(contentRoot);

    foreach (var config in new[] { "Debug", "Release" })
    {
        var compressedDir = Path.Combine(contentRoot, "obj", config, "net10.0", "compressed");
        Directory.CreateDirectory(compressedDir);
    }

    // ... rest of ConfigureWebHost
}
```

`Directory.CreateDirectory` is a no-op if the directory already exists, so it's safe
to call unconditionally. An empty `compressed/` satisfies the manifest's reference;
the tests don't actually fetch any static assets, just need the host to start.

## 2. ILGPU kernels reject Coverlet's static `HitsArray`

### Symptom

```
ILGPU.InternalCompilerException : An internal compiler error has been detected
- in method Void RecordHit(Int32) declared in type Coverlet.Core.Instrumentation.Tracker.GA.Business.Core.Analysis.Gpu_643e8ef9-...
- in method Void ComputeMagnitudeKernel(...) declared in type GA.Domain.Core.Analysis.Gpu.SetClassGpuAnalyzer
  ----> System.NotSupportedException : Cannot load from the static field 'Int32[] HitsArray' since it is not read only
```

### Root cause

CI runs `dotnet test --collect:"XPlat Code Coverage"` which activates Coverlet.
Coverlet injects a static `int[] HitsArray` field plus a `RecordHit(int)` call at
every basic block of every instrumented assembly. ILGPU's kernel compiler walks the
IL of each kernel method and rejects loads from non-readonly static fields — kernels
must be self-contained for GPU JIT. The injected `HitsArray.RecordHit(...)` violates
that constraint.

### Fix

Exclude the GPU assembly from Coverlet instrumentation at assembly scope:

```xml
<!-- Common/GA.Business.Core.Analysis.Gpu.csproj -->
<ItemGroup>
  <AssemblyAttribute Include="System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute" />
</ItemGroup>
```

Coverlet honours `[ExcludeFromCodeCoverage]` at assembly scope and skips
instrumentation entirely — no tracker static is emitted. The GPU layer disappears
from coverage reports, but it's ILGPU-glue code with its own integration tests, so
that's an acceptable trade.

Alternative: pass `Exclude="[Assembly]*"` to the XPlat collector via the runsettings,
but assembly attribute is more portable (works in any CI that calls `dotnet test`,
no runsettings plumbing).

## 3. Performance assertion too tight for shared CI runners

### Symptom

```
SearchAsync_MultipleQueries_ShouldMaintainPerformance
  Expected: less than 50
  But was:  83.109999999999999d
```

### Root cause

The test asserted `avgTimeMs < 50` on a workload that hits ~20–30ms on a dev
workstation but ~80–90ms on GitHub-hosted runners. The 50ms guard was effectively
a runner-class assertion, not a performance assertion.

### Fix

Loosen the threshold when running in CI, keep tight locally so dev-side regressions
still fail loudly:

```csharp
var threshold = Environment.GetEnvironmentVariable("CI") == "true" ? 200 : 50;
Assert.That(avgTimeMs, Is.LessThan(threshold),
    $"Average search time should be under {threshold}ms (actual: {avgTimeMs:F2}ms)");
```

A 10x regression would still trip the 200ms guard, so this catches true regressions
without flapping on neighbor-load on shared hardware.

## 4. Fixed `Thread.Sleep` racing a threadpool Timer callback

### Symptom

```
LazyWithExpirationTests.TimerStartsOnFirstAccess_NotOnConstruction
  Expected: 2
  But was:  1
```

### Root cause

The test created a `LazyWithExpiration<int>` with 100ms expiration, accessed Value
(starting the timer), slept 300ms, and asserted the second access recomputed. On a
slow CI runner the threadpool Timer callback that fires the expiration didn't run
within the 300ms window — the underlying Lazy still held the cached value.

### Fix

Switch from a fixed sleep to a spin-wait bounded by 10× expiration:

```csharp
var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(expiration.TotalMilliseconds * 10);
int second;
do
{
    Thread.Sleep(50);
    second = lazy.Value;
}
while (second == 1 && DateTime.UtcNow < deadline);

Assert.That(second, Is.EqualTo(2));
```

A genuinely broken timer (never fires) still fails. A backed-up scheduler that
takes 525ms instead of the expected 300ms passes.

## Generalizable patterns

- **WebApplicationFactory tests are sensitive to the project's MSBuild output state.**
  If the test factory hardcodes a content root, also reproduce the directory layout
  the host expects at startup (compressed/, wwwroot/, etc.) before calling
  `base.ConfigureWebHost`.
- **Code coverage and code generation don't mix.** Coverlet instruments at IL level;
  any JIT that scans the IL (ILGPU, ONNX Runtime EP, JIT-compiled regex with
  source generators) can choke on the injected tracker. `[assembly:
  ExcludeFromCodeCoverage]` is the lowest-impact escape hatch.
- **Absolute perf thresholds need an environment escape valve.** A 50ms guard that
  is tight on a dev workstation is meaningless on a GitHub-hosted runner sharing
  CPU with the entire fleet. Detect CI via the standard `CI=true` env var.
- **Don't `Thread.Sleep` past a wait — spin-wait with a bounded deadline.** A fixed
  sleep races scheduler jitter. A loop with a 10× ceiling catches the actual
  failure mode without flapping.

All four were pre-existing on `main` — they had been silently red for some weeks.
The Backend Tests rollup masked which were genuine regressions vs which were CI
environment artefacts. After this fix, Backend Tests reports cleanly so future
regressions surface immediately.
