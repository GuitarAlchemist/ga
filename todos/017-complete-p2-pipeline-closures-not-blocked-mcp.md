---
status: complete
priority: p2
issue_id: "017"
tags: [security, mcp, code-review]
dependencies: []
---

# 017 — pipeline.* Closures Not Blocked in MCP Permit List

## Problem Statement

`IsPermittedForMcp` in `GaDslTool.cs` blocks `io.*` and `agent.*` closures from MCP callers, but `pipeline.*` closures are not blocked. `pipeline.storeQdrant` (when implemented) writes to Qdrant with a caller-controlled `collection` string, and `pipeline.reportFailures` writes to stderr. MCP clients can trigger these side effects now that the stubs are live. The block list has a gap.

Additionally, `surface.transpile` is categorized as a pipeline closure but is a pure transformation — if agents legitimately need it, it should appear on an explicit allowlist rather than being implicitly accessible because the block list is incomplete.

## Findings

- `GaMcpServer/Tools/GaDslTool.cs` lines 26–28: permit check covers `io.*` and `agent.*` only.
- `pipeline.*` namespace is not mentioned in the check.
- `pipeline.storeQdrant` stub accepts a caller-supplied `collection` parameter — potential for unauthorized Qdrant writes.
- `surface.transpile` is pure but lives in the same namespace gap.

## Proposed Solutions

### Option A — Extend the block predicate
Add `pipeline.*` to the `IsPermittedForMcp` block:
```csharp
private static bool IsPermittedForMcp(string name) =>
    !name.StartsWith("io.", StringComparison.OrdinalIgnoreCase) &&
    !name.StartsWith("agent.", StringComparison.OrdinalIgnoreCase) &&
    !name.StartsWith("pipeline.", StringComparison.OrdinalIgnoreCase);
```
Then add `surface.transpile` to an explicit allowlist if MCP agents need it:
```csharp
private static readonly HashSet<string> McpAllowlist =
[
    "surface.transpile"
];

private static bool IsPermittedForMcp(string name) =>
    McpAllowlist.Contains(name, StringComparer.OrdinalIgnoreCase) ||
    (!name.StartsWith("io.", ...) &&
     !name.StartsWith("agent.", ...) &&
     !name.StartsWith("pipeline.", ...));
```
**Pros:** Closes the gap; makes allowlist intent explicit.
**Cons:** Requires updating allowlist when new safe pipeline closures are added.
**Effort:** Low.
**Risk:** Low.

### Option B — Invert to allowlist-only
Block everything not explicitly listed. Safer by default, but breaks any closure not yet enumerated.
**Pros:** Defense-in-depth; no future gaps.
**Cons:** High maintenance overhead as closure library grows; risk of breaking MCP tool callers.
**Effort:** Medium.
**Risk:** Medium (regression).

## Recommended Action

## Technical Details

- **Affected files:**
  - `GaMcpServer/Tools/GaDslTool.cs` (lines 26–28)

## Acceptance Criteria

- [ ] MCP calls to `pipeline.*` closures (e.g., `pipeline.storeQdrant`, `pipeline.reportFailures`) are rejected by `IsPermittedForMcp`.
- [ ] `surface.transpile` is accessible to MCP callers via an explicit allowlist (or is deliberately blocked if agents do not need it — decision recorded here).
- [ ] `io.*` and `agent.*` remain blocked as before.
- [ ] `dotnet build AllProjects.slnx -c Debug` passes with zero warnings.
- [ ] `dotnet test AllProjects.slnx` passes.

## Work Log

- 2026-03-07 — Identified in /ce:review of PR #2

## Resources

- PR: https://github.com/GuitarAlchemist/ga/pull/2
