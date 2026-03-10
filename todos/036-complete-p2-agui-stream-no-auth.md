---
status: complete
priority: p2
issue_id: "036"
tags: [security, authentication, authorization, code-review]
dependencies: []
---

# 036 — `POST /api/chatbot/agui/stream` Has No Authentication

## Problem Statement

`AgUiChatController` has no `[Authorize]` attribute and `Program.cs` does not call `UseAuthentication()` / `UseAuthorization()`. The global rate limiter (60 req/min per IP) provides a weak barrier but does not prevent:
- Unauthenticated callers invoking Anthropic API calls (billed externally) at will
- Exhausting the three concurrent LLM slots to deny service to authenticated users
- Bot-driven credential harvesting from leaked error messages (see 026)

CORS is set to `AllowAll` on five localhost origins — correct for dev but provides no protection in staging/production.

## Findings

`Apps/ga-server/GaApi/Controllers/AgUiChatController.cs` lines 30–33: no `[Authorize]` attribute.

`Apps/ga-server/GaApi/Program.cs`: no `app.UseAuthentication()` or `app.UseAuthorization()`.

Global rate limiter: `options.GlobalLimiter = PartitionedRateLimiter.Create(...)` at 60 req/min per IP — IP-level only, trivially bypassed.

## Proposed Solutions

### Option A — Add `[Authorize]` with bearer token or API key middleware
Register `AddAuthentication` + `AddJwtBearer` (or API key scheme) in `Program.cs`, add `[Authorize]` to the controller or the specific action.
- **Effort:** Medium — requires a token/key issuance strategy.

### Option B — Dev-only guard
Add a `IsDevelopment()` middleware check that permits all traffic in dev, requires a shared secret header in staging/prod:
```csharp
app.Use(async (ctx, next) => {
    if (!env.IsDevelopment() && !ctx.Request.Headers.ContainsKey("X-Api-Key"))
        { ctx.Response.StatusCode = 401; return; }
    await next(ctx);
});
```
- **Effort:** Small.
- **Risk:** Low — simple gate that can be replaced with full auth later.

### Option C — Document as intentionally open (dev tool)
Accept the risk with a comment explaining the design decision.
- **Effort:** Trivial.
- **Risk:** Escalates if deployed publicly.

## Recommended Action
Option B for now — unblock dev usage while preventing accidental public exposure.

## Acceptance Criteria

- [ ] Unauthenticated requests to `/api/chatbot/agui/stream` in non-Development environments return 401
- [ ] Dev environment continues to function without credentials
- [ ] Rate limiting remains in place as an additional layer

## Work Log

- 2026-03-10: Identified during security review agent for PR #8
