---
status: complete
priority: p1
issue_id: "007"
tags: [security, ssrf, mcp, code-review]
dependencies: []
---

# 007 — SSRF via `tab.fetchUrl` Closure Reachable from MCP

## Problem Statement
The `tab.fetchUrl` closure is categorized as `GaClosureCategory.Domain`, which causes it to pass the `IsPermittedForMcp` gate in `GaDslTool.cs`. Any MCP client (including external agents) can call `GaInvokeClosure("tab.fetchUrl", "{\"url\":\"http://169.254.169.254/...\"}}")` and reach internal services — Aspire dashboard, MongoDB, Qdrant, Redis, and Ollama — that are not intended to be publicly reachable. A `ContentValidator.isTrustedDomain` function exists in the codebase but is **not called** by `tabFetchUrl` before making the outbound HTTP request.

## Findings
- `GaMcpServer/Tools/GaDslTool.cs:26–28`: `IsPermittedForMcp` allows all closures whose category is `Domain`; `tab.fetchUrl` qualifies.
- `Common/GA.Business.DSL/Closures/BuiltinClosures/TabClosures.fs:239–258`: `tabFetchUrl` constructs and dispatches an `HttpClient` call directly from the user-supplied URL with no sanitization or domain allowlist check.
- `ContentValidator.isTrustedDomain` exists and is capable of blocking non-allowlisted hosts, but it is wired only to content ingestion paths, not to `tabFetchUrl`.
- Internal service addresses reachable on the dev/prod network include the AWS/GCP metadata endpoint (`169.254.169.254`), localhost ports for MongoDB, Qdrant, Redis, and Ollama.

## Proposed Solutions
### Option A — Block `tab.*` in MCP allowlist (fast mitigation)
Add `"tab."` (or the specific closure name `"tab.fetchUrl"`) to the blocklist inside `IsPermittedForMcp` so it is never exposed via the MCP surface.

**Pros:** Single-line fix; eliminates the attack vector immediately; no F# changes required.
**Cons:** Removes legitimate agent-facing tab-fetch capability; other `tab.*` closures may be safe to expose.
**Effort:** Small
**Risk:** Low

### Option B — Wire URL validation before HTTP call (correct long-term fix)
In `TabClosures.fs`, call `ContentValidator.validateUrlSafety` (or an equivalent domain-allowlist function) on the user-supplied URL before constructing the `HttpClient` request. Return an error result if the URL fails validation.

**Pros:** Preserves the capability for safe external URLs; fixes the root cause regardless of how the closure is invoked (MCP, LSP, DSL REPL).
**Cons:** Requires F# changes and a maintained allowlist; must also block RFC-1918 and link-local ranges.
**Effort:** Medium
**Risk:** Low (additive guard)

## Recommended Action
(Leave blank — to be filled during triage)

## Technical Details
- **Affected files:**
  - `GaMcpServer/Tools/GaDslTool.cs:26–28`
  - `Common/GA.Business.DSL/Closures/BuiltinClosures/TabClosures.fs:239–258`
- **Components:** MCP server, GA DSL closure engine, `ContentValidator`

## Acceptance Criteria
- [ ] An MCP call to `GaInvokeClosure("tab.fetchUrl", ...)` with a private/metadata IP is rejected before any outbound HTTP request is made.
- [ ] Rejection produces a structured error result (not an unhandled exception).
- [ ] If Option B is chosen: safe public URLs continue to work; RFC-1918, loopback, and link-local addresses are blocked.
- [ ] Unit test covering the SSRF attempt returns an error without network I/O.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
