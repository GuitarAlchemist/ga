---
status: pending
priority: p3
issue_id: "020"
tags: [security, error-handling, code-review]
dependencies: []
---

# 020 — Raw ex.Message Leaked in HTTP 500 Responses

## Problem Statement
Multiple controller catch blocks return raw `ex.Message` directly in HTTP 500 responses with no `IsDevelopment()` guard. Database connection strings, file paths, and internal service addresses from MongoDB/EF drivers can surface in API error responses visible to callers.

## Findings
`ErrorHandlingMiddleware` correctly gates `exception.ToString()` behind an `IsDevelopment()` check, but individual catch blocks in controllers bypass that middleware entirely and expose internal exception messages unconditionally.

Affected locations:
- `Apps/ga-server/GaApi/Controllers/HealthController.cs` lines 117, 140, 198, 236
- `Apps/ga-server/GaApi/Controllers/MonadicChordsController.cs` lines 53, 224, 247

## Proposed Solutions
1. Replace the raw `ex.Message` in each response body with a generic string (e.g. `"An internal error occurred."`).
2. Log the full exception to the structured logger (`ILogger`) at `Error` level so it remains observable without leaking to callers.
3. Apply the same `IsDevelopment()` env guard used in `ErrorHandlingMiddleware` if any detail is ever needed in the response body.

## Recommended Action

## Technical Details
- **Affected files:**
  - `Apps/ga-server/GaApi/Controllers/HealthController.cs` (lines 117, 140, 198, 236)
  - `Apps/ga-server/GaApi/Controllers/MonadicChordsController.cs` (lines 53, 224, 247)
  - `Apps/ga-server/GaApi/Middleware/ErrorHandlingMiddleware.cs` (reference implementation)

## Acceptance Criteria
- [ ] No catch block in any controller returns `ex.Message` or `ex.ToString()` in the response body without an `IsDevelopment()` guard.
- [ ] Each affected catch block logs the full exception via the structured logger before returning.
- [ ] A generic error message is returned to callers in production.
- [ ] Existing tests pass; add at minimum one test asserting the 500 body does not contain stack-trace or connection-string fragments.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
