---
status: complete
priority: p1
issue_id: "006"
tags: [security, rate-limiting, code-review]
dependencies: []
---

# 006 — Rate Limiter Is Global (Not Per-IP) and Never Applied to Endpoints

## Problem Statement
`Program.cs` registers a fixed-window rate limiter under the policy name `"global"` using `AddFixedWindowLimiter`. This is a single shared counter across ALL clients — it is not partitioned by IP address. Additionally, the policy is never wired to any endpoint or middleware pipeline via `.RequireRateLimiting("global")` or `app.UseRateLimiter()` with a global limiter. The practical result is that **no rate limiting is enforced on any endpoint**.

## Findings
- `Apps/ga-server/GaApi/Program.cs:128–138`: registers `AddFixedWindowLimiter("global", ...)` as a named policy only; no `options.GlobalLimiter` assignment and no `.RequireRateLimiting()` call anywhere in the file.
- One client can exhaust the shared counter and deny service to all others, or (more likely) the limiter is simply never consulted.
- The correct pattern already exists elsewhere in the repo: `GA.Fretboard.Service/Program.cs:81` uses `PartitionedRateLimiter.Create<HttpContext, string>` keyed on `RemoteIpAddress` and assigns it to `options.GlobalLimiter`.

## Proposed Solutions
### Option A — Partitioned global limiter (recommended)
Replace the named policy with a `PartitionedRateLimiter.Create<HttpContext, string>` keyed on `context.Connection.RemoteIpAddress?.ToString() ?? "unknown"` and assign the result to `options.GlobalLimiter`. This applies per-IP limiting to every request automatically without touching controllers.

**Pros:** Covers all endpoints including SignalR hubs and GraphQL; mirrors the already-correct pattern in `GA.Fretboard.Service`.
**Cons:** Requires removing or renaming the existing named policy to avoid confusion.
**Effort:** Small
**Risk:** Low

### Option B — Apply named policy per controller/hub
Keep `AddFixedWindowLimiter("global", ...)` but add `.RequireRateLimiting("global")` to each controller group and the SignalR hub route. Also change the limiter to use `PartitionedRateLimiter` internally so it is per-IP.

**Pros:** Fine-grained control per route.
**Cons:** Easy to miss new endpoints; shared-counter bug must still be fixed separately; more code to maintain.
**Effort:** Medium
**Risk:** Medium

## Recommended Action
(Leave blank — to be filled during triage)

## Technical Details
- **Affected files:**
  - `Apps/ga-server/GaApi/Program.cs:128–138`
- **Reference (correct pattern):** `GA.Fretboard.Service/Program.cs:81`
- **Components:** ASP.NET Core rate limiting middleware, all REST controllers, SignalR hubs, GraphQL endpoint

## Acceptance Criteria
- [ ] Rate limiting is partitioned by client IP (not a single shared counter).
- [ ] Every inbound HTTP request (REST, GraphQL, SignalR upgrade) passes through the limiter.
- [ ] A client that exceeds the limit receives HTTP 429; other clients are unaffected.
- [ ] Existing tests still pass; add at least one test asserting 429 on the Nth request from the same IP.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
