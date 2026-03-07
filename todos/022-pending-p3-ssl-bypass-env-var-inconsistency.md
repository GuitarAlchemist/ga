---
status: pending
priority: p3
issue_id: "022"
tags: [security, configuration, code-review]
dependencies: []
---

# 022 — SSL Bypass Checks Wrong Environment Variable

## Problem Statement
`AgentClosures.fs:16` gates the SSL/TLS bypass on `DOTNET_ENVIRONMENT = "Development"`, but ASP.NET Core's conventional variable is `ASPNETCORE_ENVIRONMENT`. This creates two failure modes:

1. If only `ASPNETCORE_ENVIRONMENT=Development` is set (the common case), the bypass is silently skipped and requests may fail in local dev.
2. If `DOTNET_ENVIRONMENT` is accidentally left as `Development` in a staging or production container image, the bypass is active in a non-development environment.

## Findings
- `Common/GA.Business.DSL/Closures/BuiltinClosures/AgentClosures.fs:16` reads `DOTNET_ENVIRONMENT`.
- ASP.NET Core reads `ASPNETCORE_ENVIRONMENT` for `IHostEnvironment.IsDevelopment()`.
- The two variables are independent; neither implies the other unless explicitly mirrored.

## Proposed Solutions
1. Check both `DOTNET_ENVIRONMENT` OR `ASPNETCORE_ENVIRONMENT` equals `"Development"` so the bypass activates under either convention.
2. Alternatively, standardize on `ASPNETCORE_ENVIRONMENT` only to match ASP.NET Core convention and update any launch profiles that set `DOTNET_ENVIRONMENT`.
3. Emit a startup warning log entry (at `Warning` level) whenever the SSL bypass is active so it is visible in dashboards and log aggregators.

## Recommended Action

## Technical Details
- **Affected files:**
  - `Common/GA.Business.DSL/Closures/BuiltinClosures/AgentClosures.fs` (line 16)

## Acceptance Criteria
- [ ] The SSL bypass activates when `ASPNETCORE_ENVIRONMENT=Development` is set.
- [ ] The SSL bypass does NOT activate when neither variable is `"Development"`.
- [ ] A `Warning`-level log message is emitted at startup whenever the bypass is active.
- [ ] Launch profiles and container environment definitions are consistent with whichever variable is chosen.
- [ ] Existing tests pass.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
