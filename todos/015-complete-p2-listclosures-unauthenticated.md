---
status: complete
priority: p2
issue_id: "015"
tags: [security, mcp, code-review]
dependencies: []
---

# 015 — GET /api/ga/closures Exposes Full Capability Roadmap Without Auth

## Problem Statement

`GET /api/ga/closures` is unauthenticated and returns the full name, description, input schema, and output type of every registered closure — including `io.*` (file read/write, HTTP) and `agent.*` closures. This gives any anonymous caller a complete enumeration of all side-effecting capabilities registered in the system. There is no `[Authorize]` attribute on the action and no `env.IsDevelopment()` guard, unlike the `Eval` action in the same controller.

## Findings

- `GaEvalController.cs` lines 49–69: the `ListClosures` action has no authorization requirement.
- The `Eval` action in the same controller is already gated behind `env.IsDevelopment()` as a precedent.
- Exposing the schema of `io.*` and `agent.*` closures to anonymous callers assists reconnaissance before exploitation.

## Proposed Solutions

### Option A — Development-only guard (same pattern as Eval)
Wrap the action body with `if (!env.IsDevelopment()) return Forbid();` — or use an `[ApiExplorerSettings(IgnoreApi = true)]` + conditional registration.
**Pros:** Consistent with existing `Eval` action pattern; zero auth infrastructure required.
**Cons:** Endpoint still accessible in dev, which may be acceptable but is worth noting.
**Effort:** Low — one-line guard.
**Risk:** Low.

### Option B — Require authorization
Add `[Authorize]` to the action (or controller class if all actions should be protected).
**Pros:** Works in all environments; proper access control.
**Cons:** Requires auth middleware to be configured for this controller; may break unauthenticated dev workflows.
**Effort:** Low.
**Risk:** Low if auth middleware already covers this route.

### Option C — Filtered response
Return only closures that pass `IsPermittedForMcp` when the caller is anonymous; return the full list only to authorized callers.
**Pros:** Usable by MCP clients without full auth.
**Cons:** More complex; filtered list still reveals structure.
**Effort:** Medium.
**Risk:** Medium.

## Recommended Action

## Technical Details

- **Affected files:**
  - `Apps/ga-server/GaApi/Controllers/GaEvalController.cs` (lines 49–69)

## Acceptance Criteria

- [ ] An unauthenticated `GET /api/ga/closures` request in a non-development environment does not return closure schemas.
- [ ] The existing `Eval` action behavior is unchanged.
- [ ] `dotnet build AllProjects.slnx -c Debug` passes with zero warnings.
- [ ] `dotnet test AllProjects.slnx` passes.

## Work Log

- 2026-03-07 — Identified in /ce:review of PR #2

## Resources

- PR: https://github.com/GuitarAlchemist/ga/pull/2
