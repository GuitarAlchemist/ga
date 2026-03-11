---
status: pending
priority: p2
issue_id: "050"
tags: [code-review, architecture, di, plugins, reliability]
---

# ChatPluginHost Assembly Scan Is Fragile and Silently Drops Plugins

## Problem Statement
`ChatPluginHost` uses `AppDomain.GetAssemblies()` to discover plugins. This approach has three compounding problems:
1. Non-deterministic assembly load order can silently miss plugins if an assembly has not yet been loaded at scan time.
2. Instantiation errors (missing constructor args, DI failures) are swallowed, so broken plugins disappear without any log entry.
3. Plugin constructors cannot receive DI-injected dependencies because the scan uses `Activator.CreateInstance` without an `IServiceProvider`.

## Proposed Solution
- Accept an explicit list (or registration delegate) of plugin types at DI registration time as the primary discovery mechanism
- Keep the `AppDomain` scan as an explicit opt-in for dynamic/late-loaded plugin scenarios only
- Instantiate plugins via `IServiceProvider` so constructors can receive injected dependencies
- Log a warning (not swallow) when a candidate type fails to instantiate

**File:** `Common/GA.Business.ML/Agents/Plugins/ChatPluginHost.cs`

## Acceptance Criteria
- [ ] Explicit plugin type registration is the default path (no implicit assembly scan)
- [ ] Assembly scan is opt-in and clearly documented
- [ ] Plugin instantiation uses `IServiceProvider`, enabling constructor injection
- [ ] Failed plugin instantiation logs a warning with the type name and exception message
- [ ] Existing plugins (`SkillMdPlugin`, etc.) register correctly under the new mechanism
- [ ] Unit test verifies that a broken plugin logs a warning and does not crash the host
