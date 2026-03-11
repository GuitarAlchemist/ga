---
status: complete
priority: p2
issue_id: "040"
tags: [architecture, startup, testability, code-review]
dependencies: []
---

# 040 — `SkillMdPlugin.Register` Performs Filesystem I/O During DI Registration

## Problem Statement

`IChatPlugin.Register` is called by `ChatPluginHost` during `IServiceCollection` setup — inside `Program.cs` / startup. `SkillMdPlugin.Register` calls `Directory.Exists`, `Directory.EnumerateFiles`, and reads every SKILL.md file at this stage. This:
- Blocks the startup thread on filesystem latency
- Makes registration order sensitive to filesystem state at startup time
- Makes unit-testing the plugin registration impossible without a real directory tree on disk

The `ChatPluginHost` justifies this as "eager so plugin registration errors surface immediately," but that logic applies to plugin instantiation, not file I/O.

## Findings

`Common/GA.Business.ML/Agents/Plugins/SkillMdPlugin.cs` lines 23–55: all file discovery and `SkillMdParser.TryParse` calls happen synchronously inside `Register()`, which is called from `ChatPluginHost.AddChatPluginHost()`.

## Proposed Solutions

### Option A — Defer file loading to `IHostedService.StartAsync` (Recommended)
Register the skills path at DI time (fast), but load and register `IOrchestratorSkill` instances in a `BackgroundService.StartAsync` where `ILogger<T>` and `IServiceProvider` are available:
```csharp
services.AddHostedService<SkillMdLoaderService>();
```
- **Pros:** Non-blocking startup; can use proper logging; testable.
- **Effort:** Medium.

### Option B — Use `IConfiguration` to get the path eagerly, load files lazily on first resolution
Register a `Lazy<IReadOnlyList<IOrchestratorSkill>>` — files are read the first time `IOrchestratorSkill` is resolved.
- **Effort:** Small.

### Option C — Accept as-is with documented rationale
SKILL.md files are few and small; startup I/O cost is negligible in practice.
- **Effort:** Zero.
- **Cons:** Untestable without a real directory.

## Recommended Action
Option A for production correctness; Option C acceptable if the test coverage gap is addressed via integration tests.

## Acceptance Criteria

- [ ] `SkillMdPlugin.Register` does not call `Directory.EnumerateFiles` or `File.ReadAllText` synchronously during DI registration
- [ ] Skills are discoverable within 1 second of application startup
- [ ] Unit tests for `SkillMdPlugin` do not require a real directory tree on disk

## Work Log

- 2026-03-10: Identified during architecture review agent for PR #8
